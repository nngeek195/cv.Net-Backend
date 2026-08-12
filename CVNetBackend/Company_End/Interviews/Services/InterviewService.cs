using Npgsql;
using Dapper;
using CVNetBackend.Company_End.Interviews.Models;
using System.Security.Cryptography;
using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace CVNetBackend.Company_End.Interviews.Services;

public class InterviewService
{
    private readonly string _connString;

    public InterviewService(IConfiguration config)
    {
        string host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        string port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        string db   = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres";
        string user = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        string pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";
        _connString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};SslMode=Require;Trust Server Certificate=true;";
    }

    public async Task<IEnumerable<InterviewCandidateDto>> GetAllInterviewsAsync(string email)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT 
                c.id::text                          AS ""CallId"",
                a.id::text                          AS ""AppId"",
                u.id::text                          AS ""UserId"",
                u.full_name                         AS ""FullName"",
                u.email                             AS ""Email"",
                u.profile_image_url                 AS ""ProfileImageUrl"",
                j.id::text                          AS ""JobId"",
                j.title                             AS ""JobTitle"",
                c.interview_date                    AS ""InterviewDate""
            FROM public.call_for_interviews c
            JOIN public.job_applications a ON c.application_id = a.id
            JOIN public.""user"" u ON c.user_id = u.id
            JOIN public.jobs j ON c.job_id = j.id
            JOIN public.companies comp ON j.company_id = comp.id
            WHERE comp.hr_email = @email
            ORDER BY c.created_at DESC;";

        return await conn.QueryAsync<InterviewCandidateDto>(sql, new { email });
    }

    public async Task<bool> ScheduleInterviewAsync(string callId, DateTime interviewDate, string email)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        
        const string sql = @"
            UPDATE public.call_for_interviews 
            SET interview_date = @interviewDate 
            WHERE id = @callId::uuid AND job_id IN (
                SELECT j.id FROM public.jobs j 
                JOIN public.companies comp ON j.company_id = comp.id 
                WHERE comp.hr_email = @email
            )";
            
        return (await conn.ExecuteAsync(sql, new { callId, interviewDate, email })) > 0;
    }

    public async Task<bool> RejectCandidateAsync(string callId, string reason, string email)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        using var trans = await conn.BeginTransactionAsync();
        try
        {
            var call = await conn.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT c.user_id, c.application_id, c.job_id, a.snapshot_id 
                  FROM public.call_for_interviews c
                  JOIN public.job_applications a ON c.application_id = a.id
                  JOIN public.jobs j ON c.job_id = j.id
                  JOIN public.companies comp ON j.company_id = comp.id
                  WHERE c.id = @callId::uuid AND comp.hr_email = @email", 
                  new { callId, email });

            if (call == null) throw new UnauthorizedAccessException("Interview record not found or unauthorized access.");

            await conn.ExecuteAsync(
                @"INSERT INTO public.reject_records (id, user_id, application_id, job_id, reason, rejected_date)
                  VALUES (uuid_generate_v4(), @uid, @aid::uuid, @jid::uuid, @reason, CURRENT_TIMESTAMP)",
                new { uid = call.user_id, aid = call.application_id, jid = call.job_id, reason }, trans);

            await conn.ExecuteAsync("DELETE FROM public.call_for_interviews WHERE id = @callId::uuid", new { callId }, trans);
            await conn.ExecuteAsync("UPDATE public.job_applications SET status = 'Rejected' WHERE id = @aid::uuid", new { aid = call.application_id }, trans);

            string[] childTables = { "snapshot_social_links", "snapshot_skills", "snapshot_experience", "snapshot_education", "snapshot_projects", "snapshot_publications", "snapshot_certifications", "snapshot_memberships", "snapshot_languages", "snapshot_teaching_experience", "snapshot_research_experience", "snapshot_awards", "snapshot_volunteers" };
            foreach (var table in childTables)
                await conn.ExecuteAsync($"DELETE FROM public.{table} WHERE snapshot_id = @sid::uuid", new { sid = call.snapshot_id }, trans);
            
            await conn.ExecuteAsync("UPDATE public.application_snapshots SET personal_statement = NULL, about_me = NULL, cv_url = NULL, portfolio_url = NULL WHERE id = @sid::uuid", new { sid = call.snapshot_id }, trans);

            await trans.CommitAsync();
            return true;
        }
        catch { await trans.RollbackAsync(); throw; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SECURE PORTAL MODULE & TIMEZONE FIXES
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<(string PortalId, string PlainPassword)> CreateSharedPortalAsync(CreatePortalRequestDto dto, string email)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        
        // 🔒 Verify all JobIds being shared belong to the user's company
        var ownedJobsCount = await conn.QueryFirstOrDefaultAsync<int>(@"
            SELECT COUNT(j.id) 
            FROM public.jobs j
            JOIN public.companies c ON j.company_id = c.id
            WHERE c.hr_email = @email AND j.id::text = ANY(@jobIds)", 
            new { email, jobIds = dto.JobIds.ToArray() });

        if (ownedJobsCount != dto.JobIds.Count) 
            throw new UnauthorizedAccessException("One or more jobs do not belong to your company.");

        using var trans = await conn.BeginTransactionAsync();
        try
        {
            string pin = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            var portalId = Guid.NewGuid().ToString();

            DateTime localDate = dto.InterviewDate.ToUniversalTime().AddHours(5).AddMinutes(30);
            string dateStr = localDate.ToString("yyyy-MM-dd");
            string expStr = localDate.AddDays(7).ToString("yyyy-MM-dd HH:mm:ss");

            await conn.ExecuteAsync(@"
                INSERT INTO public.shared_interview_portals (id, interview_date, password_hash, expires_at, created_at)
                VALUES (@pid::uuid, @date::date, @pin, @exp::timestamp, CURRENT_TIMESTAMP)",
                new { pid = portalId, date = dateStr, pin, exp = expStr }, trans);

            foreach (var jobId in dto.JobIds)
            {
                await conn.ExecuteAsync(
                    "INSERT INTO public.shared_portal_jobs (portal_id, job_id) VALUES (@pid::uuid, @jid::uuid)",
                    new { pid = portalId, jid = jobId }, trans);
            }

            await trans.CommitAsync();
            return (portalId, pin);
        }
        catch (Exception ex) { Console.WriteLine($"[CREATE PORTAL ERROR] {ex.Message}"); await trans.RollbackAsync(); throw; }
    }

    public async Task<IEnumerable<ActivePortalDto>> GetActivePortalsAsync(string email)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        var portals = await conn.QueryAsync<dynamic>(@"
            SELECT 
                p.id::text AS ""PortalId"", 
                p.interview_date::timestamp AS ""InterviewDate"",
                p.expires_at AS ""ExpiresAt"",
                p.password_hash AS ""Password"",
                j.title AS ""JobTitle""
            FROM public.shared_interview_portals p
            LEFT JOIN public.shared_portal_jobs spj ON p.id = spj.portal_id
            LEFT JOIN public.jobs j ON spj.job_id = j.id
            JOIN public.companies comp ON j.company_id = comp.id
            WHERE p.expires_at > CURRENT_TIMESTAMP AND comp.hr_email = @email
            ORDER BY p.created_at DESC", 
            new { email });

        return portals.GroupBy(p => new { p.PortalId, p.InterviewDate, p.ExpiresAt, p.Password })
            .Select(g => new ActivePortalDto
            {
                PortalId = (string)g.Key.PortalId,
                InterviewDate = (DateTime)g.Key.InterviewDate,
                ExpiresAt = (DateTime)g.Key.ExpiresAt,
                Password = (string)g.Key.Password,
                JobTitles = g.Where(x => x.JobTitle != null).Select(x => (string)x.JobTitle).ToList()
            });
    }

    public async Task<bool> DeletePortalAsync(string portalId, string email)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        
        // 🔒 Only delete if the portal has jobs owned by the requesting company
        const string sql = @"
            DELETE FROM public.shared_interview_portals 
            WHERE id = @pid::uuid AND id IN (
                SELECT spj.portal_id FROM public.shared_portal_jobs spj
                JOIN public.jobs j ON spj.job_id = j.id
                JOIN public.companies c ON j.company_id = c.id
                WHERE c.hr_email = @email
            )";
            
        return (await conn.ExecuteAsync(sql, new { pid = portalId, email })) > 0;
    }

    public async Task<bool> VerifyPortalPasswordAsync(string portalId, string plainPassword)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        var portal = await conn.QueryFirstOrDefaultAsync<dynamic>("SELECT password_hash, expires_at FROM public.shared_interview_portals WHERE id = @pid::uuid", new { pid = portalId });
        if (portal == null || (DateTime)portal.expires_at < DateTime.UtcNow) return false;
        return plainPassword == (string)portal.password_hash;
    }

    public async Task<bool> VerifyCandidateInPortalAsync(string portalId, string plainPassword, string appId)
    {
        if (!await VerifyPortalPasswordAsync(portalId, plainPassword)) return false;

        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        var count = await conn.QueryFirstOrDefaultAsync<int>(@"
            SELECT COUNT(1)
            FROM public.call_for_interviews c
            JOIN public.shared_portal_jobs spj ON c.job_id = spj.job_id
            JOIN public.shared_interview_portals p ON spj.portal_id = p.id
            WHERE p.id = @pid::uuid 
              AND c.application_id = @aid::uuid", 
            new { pid = portalId, aid = appId });

        return count > 0;
    }

    public async Task<IEnumerable<SharedPortalDataDto>> GetPortalDataAsync(string portalId, string plainPassword)
    {
        Console.WriteLine($"\n[DEBUG - SERVICE] Starting GetPortalDataAsync for Portal: {portalId}");
        
        if (!await VerifyPortalPasswordAsync(portalId, plainPassword)) 
        {
            Console.WriteLine("[DEBUG - SERVICE] 🚨 PIN Verification Failed!");
            throw new UnauthorizedAccessException("Unauthorized: Invalid PIN or expired portal.");
        }
        
        Console.WriteLine("[DEBUG - SERVICE] PIN Verified. Fetching portal info...");

        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        var portalInfo = await conn.QueryFirstOrDefaultAsync<dynamic>("SELECT interview_date::text FROM public.shared_interview_portals WHERE id = @pid::uuid", new { pid = portalId });
        
        if (portalInfo == null) 
        {
            Console.WriteLine("[DEBUG - SERVICE] 🚨 Portal not found in DB!");
            throw new Exception("Portal not found.");
        }
        
        string dateStr = (string)portalInfo.interview_date;
        Console.WriteLine($"[DEBUG - SERVICE] Portal Date found: {dateStr}");

        var jobs = await conn.QueryAsync<SharedPortalDataDto>(@"
            SELECT j.id::text AS ""JobId"", j.title AS ""JobTitle""
            FROM public.jobs j
            JOIN public.shared_portal_jobs spj ON j.id = spj.job_id
            WHERE spj.portal_id = @pid::uuid", new { pid = portalId });

        var resultList = jobs.ToList();
        Console.WriteLine($"[DEBUG - SERVICE] Found {resultList.Count} jobs linked to this portal.");

        foreach (var job in resultList)
        {
            Console.WriteLine($"\n[DEEP DEBUG] --- TRACING DATA FOR JOB: {job.JobTitle} ({job.JobId}) ---");

            var callsCount = await conn.QueryFirstOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM public.call_for_interviews WHERE job_id = @jid::uuid",
                new { jid = job.JobId });
            Console.WriteLine($"[DEEP DEBUG] 1. Total raw interviews for this job: {callsCount}");

            if (callsCount > 0)
            {
                var appsCount = await conn.QueryFirstOrDefaultAsync<int>(@"
                    SELECT COUNT(*) FROM public.call_for_interviews c
                    JOIN public.job_applications a ON c.application_id = a.id
                    WHERE c.job_id = @jid::uuid", new { jid = job.JobId });
                Console.WriteLine($"[DEEP DEBUG] 2. Matches after JOIN with job_applications: {appsCount}");

                var snapsCount = await conn.QueryFirstOrDefaultAsync<int>(@"
                    SELECT COUNT(*) FROM public.call_for_interviews c
                    JOIN public.job_applications a ON c.application_id = a.id
                    JOIN public.application_snapshots s ON a.snapshot_id = s.id
                    WHERE c.job_id = @jid::uuid", new { jid = job.JobId });
                Console.WriteLine($"[DEEP DEBUG] 3. Matches after JOIN with application_snapshots: {snapsCount}");
                
                var usersCount = await conn.QueryFirstOrDefaultAsync<int>(@"
                    SELECT COUNT(*) FROM public.call_for_interviews c
                    JOIN public.job_applications a ON c.application_id = a.id
                    JOIN public.application_snapshots s ON a.snapshot_id = s.id
                    JOIN public.""user"" u ON c.user_id = u.id
                    WHERE c.job_id = @jid::uuid", new { jid = job.JobId });
                Console.WriteLine($"[DEEP DEBUG] 4. Matches after JOIN with user table: {usersCount}");
            }
            
            Console.WriteLine("[DEEP DEBUG] ----------------------------------------------------\n");

            var candidates = await conn.QueryAsync<PortalCandidateDto>(@"
                SELECT 
                    a.id::text AS ""AppId"", 
                    u.full_name AS ""FullName"", 
                    u.email AS ""Email"", 
                    u.profile_image_url AS ""ProfileImageUrl"",
                    s.industry_score AS ""IndustryScore"", 
                    c.interview_date AS ""InterviewTime""
                FROM public.call_for_interviews c
                JOIN public.job_applications a ON c.application_id = a.id
                JOIN public.""user"" u ON c.user_id = u.id
                JOIN public.application_snapshots s ON a.snapshot_id = s.id
                WHERE c.job_id = @jid::uuid", 
                new { jid = job.JobId });

            job.Candidates = candidates.ToList();
        }

        return resultList.Where(j => j.Candidates.Any());
    }
}
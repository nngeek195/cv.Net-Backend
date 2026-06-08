using Npgsql;
using Dapper;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using CVNetBackend.Company_End.ApplicationsView.Models;
using System.Linq;

namespace CVNetBackend.Company_End.ApplicationsView.Services;

public class JobDetailsService
{
    private readonly string _connString;

    public JobDetailsService(IConfiguration config)
    {
        string host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        string port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        string db   = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres";
        string user = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        string pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";
        _connString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};SslMode=Require;Trust Server Certificate=true;";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Job dashboard
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<JobSpecDto> GetJobDetailsAsync(string jobId)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT 
                j.id::text                                              AS ""Id"",
                j.title                                                 AS ""Title"",
                jc.name                                                 AS ""Dept"",
                j.location                                              AS ""Location"",
                j.salary_range                                          AS ""SalaryRange"",
                j.currency                                              AS ""Currency"",
                j.employment_type::text                                 AS ""EmploymentType"",
                j.workplace_type::text                                  AS ""WorkplaceType"",
                j.description                                           AS ""Description"",
                j.responsibilities                                      AS ""Responsibilities"",
                j.status                                                AS ""Status"",
                j.openings                                              AS ""Openings"",
                to_char(j.created_at, 'Mon DD, YYYY')                  AS ""Posted"",
                EXTRACT(DAY FROM (CURRENT_TIMESTAMP - j.created_at))::int AS ""DaysActive"",
                (SELECT COUNT(*) FROM public.job_applications WHERE job_id = j.id)::int AS ""TotalApplicants"",
                (SELECT COUNT(*) FROM public.job_applications WHERE job_id = j.id AND status = 'Pending')::int AS ""NewApplied"",
                COALESCE((
                    SELECT ROUND(AVG(s.industry_score))
                    FROM public.job_applications ja
                    JOIN public.application_snapshots s ON ja.snapshot_id = s.id
                    WHERE ja.job_id = j.id
                ), 0)::int AS ""AvgMatchScore""
            FROM public.jobs j
            JOIN public.job_categories jc ON j.job_category_id = jc.id
            WHERE j.id = @jobId::uuid;";

        var job = await conn.QueryFirstOrDefaultAsync<JobSpecDto>(sql, new { jobId });
        if (job != null)
        {
            var skills = await conn.QueryAsync<SkillReqDto>(
                @"SELECT skill_name AS ""Name"", required_level AS ""Level"" FROM public.job_skills WHERE job_id = @jobId::uuid",
                new { jobId });
            job.Skills = skills.ToList();

            var edu = await conn.QueryAsync<string>(
                "SELECT degree_name FROM public.job_education WHERE job_id = @jobId::uuid",
                new { jobId });
            job.Education = edu.ToList();

            job.Experience = await conn.QueryFirstOrDefaultAsync<ExpReqDto>(
                @"SELECT level_name AS ""LevelName"", min_years AS ""MinYears"", max_years AS ""MaxYears"" FROM public.job_experience WHERE job_id = @jobId::uuid LIMIT 1",
                new { jobId });
        }
        return job ?? new JobSpecDto();
    }

    // Replace your GetApplicantsAsync and GetFullApplicantProfileAsync methods with these:

public async Task<IEnumerable<ApplicantDto>> GetApplicantsAsync(string jobId)
{
    using var conn = new NpgsqlConnection(_connString);
    await conn.OpenAsync();

    const string sql = @"
        SELECT 
            a.id::text                          AS ""AppId"",
            u.id::text                          AS ""UserId"",
            u.full_name                         AS ""FullName"",
            u.email                             AS ""Email"",
            u.phone                             AS ""Phone"",
            u.profile_image_url                 AS ""ProfileImageUrl"", -- ADDED Profile Image mapping
            a.status                            AS ""Status"",
            s.industry_score                    AS ""IndustryScore"",
            s.company_skill_match_score         AS ""CompanyMatchScore"",
            s.id::text                          AS ""SnapshotId""
        FROM public.job_applications a
        JOIN public.""user"" u ON a.user_id = u.id
        JOIN public.application_snapshots s ON a.snapshot_id = s.id
        WHERE a.job_id = @jobId::uuid
        ORDER BY s.company_skill_match_score DESC;";

    var applicants = (await conn.QueryAsync<dynamic>(sql, new { jobId })).ToList();
    var result = new List<ApplicantDto>();

    foreach (var app in applicants)
    {
        var dto = new ApplicantDto
        {
            AppId = app.AppId, UserId = app.UserId, FullName = app.FullName,
            Email = app.Email, Phone = app.Phone, Status = app.Status,
            ProfileImageUrl = app.ProfileImageUrl, // Applying Image mapping
            IndustryScore = app.IndustryScore, CompanyMatchScore = app.CompanyMatchScore
        };
        var skills = await conn.QueryAsync<string>(
            "SELECT skill_name FROM public.snapshot_skills WHERE snapshot_id = @sid::uuid",
            new { sid = app.SnapshotId });
        dto.Skills = skills.ToList();
        result.Add(dto);
    }
    return result;
}

public async Task<FullApplicantProfileDto?> GetFullApplicantProfileAsync(string appId)
{
    using var conn = new NpgsqlConnection(_connString);
    await conn.OpenAsync();

    const string sql = @"
        -- [1] Core profile (Maps to DTO, so we use PascalCase for Dapper to auto-map)
        SELECT
            a.id::text                          AS ""AppId"",
            u.full_name                         AS ""FullName"",
            u.profile_image_url                 AS ""ProfileImageUrl"",
            u.email                             AS ""Email"",
            u.phone                             AS ""Phone"",
            u.gpa                               AS ""Gpa"",
            s.job_role                          AS ""JobRole"",
            s.current_org                       AS ""CurrentOrg"",
            s.current_position                  AS ""CurrentPosition"",
            s.match_score                       AS ""MatchScore"",
            s.industry_score                    AS ""IndustryScore"",
            s.company_skill_match_score         AS ""CompanySkillMatchScore"",
            s.personal_statement                AS ""PersonalStatement"",
            s.about_me                          AS ""AboutMe"",
            s.cv_url                            AS ""CvUrl"",
            s.portfolio_url                     AS ""PortfolioUrl""
        FROM public.job_applications a
        JOIN public.""user""              u ON a.user_id    = u.id
        JOIN public.application_snapshots s ON a.snapshot_id = s.id
        WHERE a.id = @appId::uuid;

        -- For all dynamic lists below, we explicitly map to ""camelCase"" to match frontend expectations

        -- [2] Experience
        SELECT 
            company_name as ""companyName"", 
            start_date as ""startDate"", 
            end_date as ""endDate"", 
            role_description as ""roleDescription"" 
        FROM public.snapshot_experience WHERE snapshot_id = (SELECT snapshot_id FROM public.job_applications WHERE id = @appId::uuid);

        -- [3] Education
        SELECT 
            degree_title as ""degreeTitle"", 
            field_of_study as ""fieldOfStudy"", 
            organization, 
            start_date as ""startDate"", 
            end_date as ""endDate"", 
            honors, 
            thesis_title as ""thesisTitle"", 
            relevant_coursework as ""relevantCoursework"" 
        FROM public.snapshot_education WHERE snapshot_id = (SELECT snapshot_id FROM public.job_applications WHERE id = @appId::uuid);

        -- [4] Skills
        SELECT 
            skill_name as ""skillName"", 
            level 
        FROM public.snapshot_skills WHERE snapshot_id = (SELECT snapshot_id FROM public.job_applications WHERE id = @appId::uuid);

        -- [5] Projects
        SELECT 
            name, 
            description, 
            time_period as ""timePeriod"", 
            role, 
            organization, 
            source_link as ""sourceLink"" 
        FROM public.snapshot_projects WHERE snapshot_id = (SELECT snapshot_id FROM public.job_applications WHERE id = @appId::uuid);

        -- [6] Publications
        SELECT 
            title, 
            description, 
            source_link as ""sourceLink"", 
            organization, 
            year 
        FROM public.snapshot_publications WHERE snapshot_id = (SELECT snapshot_id FROM public.job_applications WHERE id = @appId::uuid);

        -- [7] Certifications
        SELECT 
            organization, 
            field, 
            issue_date as ""issueDate"" 
        FROM public.snapshot_certifications WHERE snapshot_id = (SELECT snapshot_id FROM public.job_applications WHERE id = @appId::uuid);

        -- [8] Memberships
        SELECT 
            organization_name as ""organizationName"" 
        FROM public.snapshot_memberships WHERE snapshot_id = (SELECT snapshot_id FROM public.job_applications WHERE id = @appId::uuid);

        -- [9] Languages
        SELECT 
            language_name as ""languageName"", 
            proficiency 
        FROM public.snapshot_languages WHERE snapshot_id = (SELECT snapshot_id FROM public.job_applications WHERE id = @appId::uuid);

        -- [10] Teaching Experience
        SELECT 
            courses_taught as ""coursesTaught"", 
            organization, 
            time_period as ""timePeriod"", 
            curriculum_description as ""curriculumDescription"" 
        FROM public.snapshot_teaching_experience WHERE snapshot_id = (SELECT snapshot_id FROM public.job_applications WHERE id = @appId::uuid);

        -- [11] Research Experience
        SELECT 
            project_name as ""projectName"", 
            organization, 
            results_description as ""resultsDescription"", 
            lab_or_field_work as ""labOrFieldWork"", 
            linked_publication_title as ""linkedPublicationTitle"" 
        FROM public.snapshot_research_experience WHERE snapshot_id = (SELECT snapshot_id FROM public.job_applications WHERE id = @appId::uuid);

        -- [12] Awards
        SELECT 
            award_name as ""awardName"", 
            organization, 
            description 
        FROM public.snapshot_awards WHERE snapshot_id = (SELECT snapshot_id FROM public.job_applications WHERE id = @appId::uuid);

        -- [13] Volunteers
        SELECT 
            organization, 
            role, 
            description 
        FROM public.snapshot_volunteers WHERE snapshot_id = (SELECT snapshot_id FROM public.job_applications WHERE id = @appId::uuid);

        -- [14] Social Links
        SELECT 
            platform_name as ""platformName"", 
            profile_url as ""profileUrl"" 
        FROM public.snapshot_social_links WHERE snapshot_id = (SELECT snapshot_id FROM public.job_applications WHERE id = @appId::uuid);
    ";

    using var multi = await conn.QueryMultipleAsync(sql, new { appId });

    var profile = await multi.ReadFirstOrDefaultAsync<FullApplicantProfileDto>();
    if (profile == null) return null;

    profile.Experience         = (await multi.ReadAsync<dynamic>()).ToList();
    profile.Education          = (await multi.ReadAsync<dynamic>()).ToList();
    profile.Skills             = (await multi.ReadAsync<dynamic>()).ToList();
    profile.Projects           = (await multi.ReadAsync<dynamic>()).ToList();
    profile.Publications       = (await multi.ReadAsync<dynamic>()).ToList();
    profile.Certifications     = (await multi.ReadAsync<dynamic>()).ToList();
    profile.Memberships        = (await multi.ReadAsync<dynamic>()).ToList();
    profile.Languages          = (await multi.ReadAsync<dynamic>()).ToList();
    profile.TeachingExperience = (await multi.ReadAsync<dynamic>()).ToList();
    profile.ResearchExperience = (await multi.ReadAsync<dynamic>()).ToList();
    profile.Awards             = (await multi.ReadAsync<dynamic>()).ToList();
    profile.Volunteers         = (await multi.ReadAsync<dynamic>()).ToList();   
    profile.SocialLinks        = (await multi.ReadAsync<dynamic>()).ToList();

    return profile;
}
    private async Task WipeSnapshotDataAsync(NpgsqlConnection conn, string snapshotId, NpgsqlTransaction trans)
    {
        string[] childTables = {
            "snapshot_social_links", "snapshot_skills", "snapshot_experience",
            "snapshot_education", "snapshot_projects", "snapshot_publications",
            "snapshot_certifications", "snapshot_memberships", "snapshot_languages",
            "snapshot_teaching_experience", "snapshot_research_experience",
            "snapshot_awards", "snapshot_volunteers"
        };
        foreach (var table in childTables)
            await conn.ExecuteAsync($"DELETE FROM public.{table} WHERE snapshot_id = @sid::uuid", new { sid = snapshotId }, trans);

        await conn.ExecuteAsync(
            @"UPDATE public.application_snapshots
              SET personal_statement = NULL, about_me = NULL, cv_url = NULL, portfolio_url = NULL
              WHERE id = @sid::uuid", new { sid = snapshotId }, trans);
    }

    public async Task<bool> CloseJobAndRejectPendingAsync(string jobId, string? hrEmail)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        using var trans = await conn.BeginTransactionAsync();
        try
        {
            await conn.ExecuteAsync("UPDATE public.jobs SET status = 0 WHERE id = @jobId::uuid", new { jobId }, trans);

            var pendingApps = await conn.QueryAsync<dynamic>(
                "SELECT id::text, user_id::text, snapshot_id::text FROM public.job_applications WHERE job_id = @jobId::uuid AND status = 'Pending'",
                new { jobId }, trans);

            foreach (var app in pendingApps)
            {
                // Removed ::uuid from @uid
                await conn.ExecuteAsync(
                    @"INSERT INTO public.reject_records (id, user_id, application_id, job_id, reason, rejected_date)
                      VALUES (uuid_generate_v4(), @uid, @aid::uuid, @jobId::uuid, 'Job Closed', CURRENT_TIMESTAMP)",
                    new { uid = app.user_id, aid = app.id, jobId }, trans);

                await conn.ExecuteAsync(
                    "UPDATE public.job_applications SET status = 'Rejected' WHERE id = @aid::uuid",
                    new { aid = app.id }, trans);

                await WipeSnapshotDataAsync(conn, app.snapshot_id, trans);
            }
            await trans.CommitAsync();
            return true;
        }
        catch { await trans.RollbackAsync(); throw; }
    }

    public async Task<bool> CallForInterviewAsync(string appId, InterviewRequestDto dto)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        using var trans = await conn.BeginTransactionAsync();
        try
        {
            var app = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT user_id::text as uid, job_id::text as jid FROM public.job_applications WHERE id = @appId::uuid",
                new { appId });
            if (app == null) throw new Exception("Application not found.");

            await conn.ExecuteAsync(
                "UPDATE public.job_applications SET status = 'Interview' WHERE id = @appId::uuid",
                new { appId }, trans);

            // Removed ::uuid from @uid
            await conn.ExecuteAsync(
                @"INSERT INTO public.call_for_interviews (id, user_id, application_id, job_id, interview_date, message, created_at)
                  VALUES (uuid_generate_v4(), @uid, @aid::uuid, @jid::uuid, @date, @msg, CURRENT_TIMESTAMP)",
                new { uid = app.uid, aid = appId, jid = app.jid, date = dto.InterviewDate, msg = dto.Message }, trans);

            await trans.CommitAsync();
            return true;
        }
        catch { await trans.RollbackAsync(); throw; }
    }

    public async Task<bool> RejectApplicantAsync(string appId, RejectRequestDto dto)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        using var trans = await conn.BeginTransactionAsync();
        try
        {
            var app = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT user_id::text as uid, job_id::text as jid, snapshot_id::text as sid FROM public.job_applications WHERE id = @appId::uuid",
                new { appId });
            if (app == null) throw new Exception("Application not found.");

            await conn.ExecuteAsync(
                "UPDATE public.job_applications SET status = 'Rejected' WHERE id = @appId::uuid",
                new { appId }, trans);

            // Removed ::uuid from @uid
            await conn.ExecuteAsync(
                @"INSERT INTO public.reject_records (id, user_id, application_id, job_id, reason, rejected_date)
                  VALUES (uuid_generate_v4(), @uid, @aid::uuid, @jid::uuid, @reason, CURRENT_TIMESTAMP)",
                new { uid = app.uid, aid = appId, jid = app.jid, reason = dto.Reason }, trans);

            await WipeSnapshotDataAsync(conn, app.sid, trans);
            await trans.CommitAsync();
            return true;
        }
        catch { await trans.RollbackAsync(); throw; }
    }
    public async Task<string> RepostJobAsync(string jobId)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        using var trans = await conn.BeginTransactionAsync();
        try
        {
            var newJobId = Guid.NewGuid();
            await conn.ExecuteAsync(@"
                INSERT INTO public.jobs (id, company_id, job_category_id, title, employment_type, workplace_type,
                    location, openings, description, responsibilities, salary_range, status, currency,
                    application_deadline, expire_date, hr_contact_email, created_at)
                SELECT @newJobId, company_id, job_category_id, title, employment_type, workplace_type,
                    location, openings, description, responsibilities, salary_range, 1, currency,
                    CURRENT_TIMESTAMP + INTERVAL '30 days', CURRENT_TIMESTAMP + INTERVAL '30 days',
                    hr_contact_email, CURRENT_TIMESTAMP
                FROM public.jobs WHERE id = @jobId::uuid", new { newJobId, jobId }, trans);

            await conn.ExecuteAsync(
                @"INSERT INTO public.job_skills (id, job_id, skill_name, required_level, is_visible, show_level)
                  SELECT uuid_generate_v4(), @newJobId, skill_name, required_level, is_visible, show_level
                  FROM public.job_skills WHERE job_id = @jobId::uuid", new { newJobId, jobId }, trans);

            await conn.ExecuteAsync(
                @"INSERT INTO public.job_education (id, job_id, degree_name)
                  SELECT uuid_generate_v4(), @newJobId, degree_name FROM public.job_education WHERE job_id = @jobId::uuid",
                new { newJobId, jobId }, trans);

            await conn.ExecuteAsync(
                @"INSERT INTO public.job_experience (id, job_id, level_name, min_years, max_years)
                  SELECT uuid_generate_v4(), @newJobId, level_name, min_years, max_years
                  FROM public.job_experience WHERE job_id = @jobId::uuid", new { newJobId, jobId }, trans);

            await trans.CommitAsync();
            return newJobId.ToString();
        }
        catch { await trans.RollbackAsync(); throw; }
    }

}
using Npgsql;
using Dapper;

namespace CVNetBackend.Company_End.CandidateSection.Services;
using CVNetBackend.Company_End.CandidateSection.Models;

public class CandidateService
{
    private readonly string _connString;

    public CandidateService(IConfiguration config)
    {
        string host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        string port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        string db   = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres";
        string user = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        string pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";
        _connString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};SslMode=Require;Trust Server Certificate=true;";
    }

    public async Task<IEnumerable<JobFilterDto>> GetActiveJobsAsync()
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        // Assuming you want to show jobs that have applicants or are active
        return await conn.QueryAsync<JobFilterDto>(
            @"SELECT id::text AS ""Id"", title AS ""Title"" 
              FROM public.jobs 
              ORDER BY created_at DESC;"
        );
    }

    public async Task<IEnumerable<CandidateListDto>> GetCandidatesAsync(string? jobId, string? sortOrder, string? search)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        string orderClause = "ORDER BY s.industry_score DESC";
        if (sortOrder == "asc") orderClause = "ORDER BY s.industry_score ASC";
        else if (sortOrder == "gpa_desc") orderClause = "ORDER BY u.gpa DESC NULLS LAST";
        else if (sortOrder == "gpa_asc") orderClause = "ORDER BY u.gpa ASC NULLS FIRST";

        string sql = $@"
            SELECT 
                a.id::text                          AS ""AppId"",
                u.id::text                          AS ""UserId"",
                u.full_name                         AS ""FullName"",
                u.email                             AS ""Email"",
                u.profile_image_url                 AS ""ProfileImageUrl"",
                j.title                             AS ""JobTitle"",
                s.industry_score                    AS ""IndustryScore"",
                a.status                            AS ""Status"",
                s.id::text                          AS ""SnapshotId""
            FROM public.job_applications a
            JOIN public.""user"" u ON a.user_id = u.id
            JOIN public.application_snapshots s ON a.snapshot_id = s.id
            JOIN public.jobs j ON a.job_id = j.id
            WHERE 1=1
            {(string.IsNullOrEmpty(jobId) ? "" : " AND a.job_id = @jobId::uuid")}
            {(string.IsNullOrEmpty(search) ? "" : @" AND (
                u.full_name ILIKE @search 
                OR u.email ILIKE @search 
                OR EXISTS (SELECT 1 FROM public.snapshot_education ed WHERE ed.snapshot_id = s.id AND ed.organization ILIKE @search)
            )")}
            {orderClause};";
        var parameters = new { 
            jobId, 
            search = string.IsNullOrEmpty(search) ? null : $"%{search}%" 
        };

        var rawCandidates = (await conn.QueryAsync<dynamic>(sql, parameters)).ToList();
        var result = new List<CandidateListDto>();

        foreach (var row in rawCandidates)
        {
            var dto = new CandidateListDto
            {
                AppId = row.AppId,
                UserId = row.UserId,
                FullName = row.FullName,
                Email = row.Email,
                ProfileImageUrl = row.ProfileImageUrl,
                JobTitle = row.JobTitle,
                IndustryScore = row.IndustryScore, // Correctly mapping the dynamic property
                Status = row.Status
            };

            var skills = await conn.QueryAsync<string>(
                "SELECT skill_name FROM public.snapshot_skills WHERE snapshot_id = @sid::uuid LIMIT 5",
                new { sid = row.SnapshotId });
                
            dto.Skills = skills.ToList();
            result.Add(dto);
        }

        return result;
    }
}
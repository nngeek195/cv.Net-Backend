using Npgsql;
using Dapper;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using CVNetBackend.Company_End.JobManagement.Models;

namespace CVNetBackend.Company_End.JobManagement.Services;

public class CompanyJobService
{
    private readonly string _connString;

    public CompanyJobService(IConfiguration config)
    {
        string host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        string port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        string db = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres";
        string user = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        string pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";
        _connString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};SslMode=Require;Trust Server Certificate=true;";
    }

    public async Task<IEnumerable<CompanyJobListDto>> GetCompanyJobsAsync(string userEmail)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        string sql = @"
            SELECT 
                j.id::text as ""Id"",
                j.title as ""Title"",
                jc.name as ""Dept"",
                
                -- Calculate days ago for 'Posted'
                CASE 
                    WHEN CURRENT_DATE = j.created_at::date THEN 'Today'
                    WHEN CURRENT_DATE - j.created_at::date = 1 THEN '1 day ago'
                    ELSE (CURRENT_DATE - j.created_at::date)::text || ' days ago'
                END as ""Posted"",
                
                (SELECT COUNT(*) FROM public.job_applications ja WHERE ja.job_id = j.id) as ""Applicants"",
                
                (SELECT COUNT(*) FROM public.job_applications ja WHERE ja.job_id = j.id AND ja.status = 'Pending') as ""NewApplicants"",
                
                -- ✅ FIXED: Calculate the average industry_score across all applicants for this job
                COALESCE((
                    SELECT ROUND(AVG(s.industry_score))
                    FROM public.job_applications ja
                    JOIN public.application_snapshots s ON ja.snapshot_id = s.id
                    WHERE ja.job_id = j.id
                ), 0)::int as ""MatchAvg"",
                
                -- Determine Status based on expiration date
                CASE 
                    WHEN j.expire_date > CURRENT_TIMESTAMP THEN 'Active'
                    ELSE 'Closed'
                END as ""Status""

            FROM public.jobs j
            JOIN public.job_categories jc ON j.job_category_id = jc.id
            JOIN public.companies c ON j.company_id = c.id
            WHERE c.hr_email = @userEmail
            ORDER BY j.created_at DESC;
        ";

        return await conn.QueryAsync<CompanyJobListDto>(sql, new { userEmail });
    }
}
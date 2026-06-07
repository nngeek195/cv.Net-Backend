using Npgsql;
using Dapper;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

using CVNetBackend.User_End.JobApply.Models; 

namespace CVNetBackend.User_End.JobApply.Services;

public class CandidateJobService
{
    private readonly string _connString;

    public CandidateJobService(IConfiguration config)
    {
        string host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        string port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        string db = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres";
        string user = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        string pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";
        _connString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};SslMode=Require;Trust Server Certificate=true;";
    }

    public async Task<IEnumerable<JobCategoryDto>> GetCategoriesAsync()
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        return await conn.QueryAsync<JobCategoryDto>("SELECT id::text, name FROM public.job_categories ORDER BY name;");
    }

    // Change the method signature to accept userId
public async Task<IEnumerable<CandidateJobListingDto>> GetActiveJobsAsync(string userId)
{
    using var conn = new NpgsqlConnection(_connString);
    await conn.OpenAsync();

    string sql = @"
        SELECT 
            j.id::text, 
            j.title, 
            c.name as CompanyName, 
            c.logo_url as CompanyLogo, 
            jc.name as CategoryName, 
            j.location, 
            j.workplace_type::text as WorkplaceType, 
            j.employment_type::text as EmploymentType, 
            j.salary_range as SalaryRange, 
            j.currency, 
            j.description, 
            j.responsibilities, 
            j.created_at as CreatedAt,
            
            COALESCE((SELECT jsonb_agg(jsonb_build_object('name', s.skill_name, 'level', s.required_level, 'showLevel', s.show_level)) 
             FROM public.job_skills s WHERE s.job_id = j.id AND s.is_visible = true), '[]') as SkillsJson,
             
            COALESCE((SELECT jsonb_agg(jsonb_build_object('degree', e.degree_name)) 
             FROM public.job_education e WHERE e.job_id = j.id), '[]') as EducationsJson,
             
            COALESCE((SELECT jsonb_build_object('level', ex.level_name, 'min', ex.min_years, 'max', ex.max_years) 
             FROM public.job_experience ex WHERE ex.job_id = j.id LIMIT 1), '{}') as ExperienceJson

        FROM public.jobs j
        JOIN public.companies c ON j.company_id = c.id
        JOIN public.job_categories jc ON j.job_category_id = jc.id
        WHERE j.expire_date > CURRENT_TIMESTAMP
          -- ✅ DO NOT show jobs the user has already applied for
          AND NOT EXISTS (
              SELECT 1 FROM public.job_applications ja 
              WHERE ja.job_id = j.id AND ja.user_id = @userId
          )
        ORDER BY j.created_at DESC;
    ";

    return await conn.QueryAsync<CandidateJobListingDto>(sql, new { userId });
    }
}
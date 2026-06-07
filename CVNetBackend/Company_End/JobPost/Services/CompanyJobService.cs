using Npgsql;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using CVNetBackend.Company_End.Models;

namespace CVNetBackend.Company_End.Services;

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

    public async Task<object> GetCategoriesAndRolesAsync()
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        var sql = @"
            SELECT 
                c.id as category_id, 
                c.name as category_name, 
                g.job_role
            FROM public.job_categories c
            LEFT JOIN public.general_skills g ON c.id = g.job_category_id
        ";

        var rows = await conn.QueryAsync<dynamic>(sql);
        
        var result = rows.GroupBy(r => new { r.category_id, r.category_name })
                         .Select(g => new {
                             Id = g.Key.category_id?.ToString(),
                             Name = g.Key.category_name?.ToString(),
                             Roles = g.Where(x => x.job_role != null).Select(x => x.job_role?.ToString()).Distinct().ToList()
                         }).ToList();
        
        return result;
    }

    public async Task<string> CreateJobAsync(string userEmail, CreateJobDto dto)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        using var trans = await conn.BeginTransactionAsync();

        try 
        {
            var companyId = await conn.QueryFirstOrDefaultAsync<Guid?>(
                @"SELECT id FROM public.companies WHERE hr_email = @email LIMIT 1", 
                new { email = userEmail });

            if (companyId == null) throw new Exception("Company profile not found for this user.");

            var jobId = Guid.NewGuid();
            string jobSql = @"
                INSERT INTO public.jobs 
                (id, company_id, job_category_id, title, employment_type, workplace_type, location, openings, description, responsibilities, salary_range, currency, application_deadline, expire_date, hr_contact_email, created_at)
                VALUES 
                (@id, @companyId, @jobCategoryId::uuid, @title, @empType::employment_type, @wpType::workplace_type, @loc, @openings, @desc, @resp, @sal, @curr, @deadline::timestamp, @expire::timestamp, @hrEmail, CURRENT_TIMESTAMP);
            ";

            await conn.ExecuteAsync(jobSql, new {
                id = jobId,
                companyId = companyId,
                jobCategoryId = dto.CategoryId,
                title = dto.JobTitle,
                empType = dto.EmploymentType,
                wpType = dto.WorkplaceType,
                loc = dto.Location,
                openings = dto.Openings,
                // ✅ FIXED: Coalesce null values to empty strings to satisfy the NOT NULL constraint!
                desc = dto.Description ?? "", 
                resp = dto.Responsibilities ?? "",
                sal = dto.SalaryRange,
                curr = dto.Currency,
                deadline = dto.ApplicationDeadline,
                expire = dto.ApplicationDeadline.AddDays(30), 
                hrEmail = dto.HrContactEmail
            }, trans);

            if (dto.Skills.Any()) 
            {
                string skillSql = @"
                    INSERT INTO public.job_skills 
                    (id, job_id, skill_name, required_level, is_visible, show_level) 
                    VALUES (@id, @jobId, @name, @lvl::skill_level_requirement, @isVisible, @showLevel)";
                    
                foreach(var s in dto.Skills) {
                    await conn.ExecuteAsync(skillSql, new { 
                        id = Guid.NewGuid(), 
                        jobId = jobId, 
                        name = s.Name, 
                        lvl = s.Level,
                        isVisible = s.IsVisible,
                        showLevel = s.ShowLevel
                    }, trans);
                }
            }

            if (dto.Experience != null) 
            {
                string expSql = @"INSERT INTO public.job_experience (id, job_id, level_name, min_years, max_years) VALUES (@id, @jobId, @lvl, @min, @max)";
                await conn.ExecuteAsync(expSql, new { id = Guid.NewGuid(), jobId = jobId, lvl = dto.Experience.LevelName, min = dto.Experience.MinYears, max = dto.Experience.MaxYears }, trans);
            }

            if (dto.Educations.Any()) 
            {
                string eduSql = @"INSERT INTO public.job_education (id, job_id, degree_name) VALUES (@id, @jobId, @deg)";
                foreach(var e in dto.Educations) {
                    await conn.ExecuteAsync(eduSql, new { id = Guid.NewGuid(), jobId = jobId, deg = e }, trans);
                }
            }

            await trans.CommitAsync();
            return jobId.ToString();
        } 
        catch 
        {
            await trans.RollbackAsync();
            throw;
        }
    }
}
using Npgsql;
using Dapper;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using CVNetBackend.User_End.JobApply.Models;
using System.Text.Json;

namespace CVNetBackend.User_End.JobApply.Services;

public class ApplicationService
{
    private readonly string _connString;

    public ApplicationService(IConfiguration config)
    {
        string host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        string port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        string db = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres";
        string user = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        string pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";
        _connString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};SslMode=Require;Trust Server Certificate=true;";
    }

    public async Task<IEnumerable<TargetProfileDropdownDto>> GetUserProfilesAsync(string userId)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        string sql = "SELECT id::text, job_role as JobRole, personal_statement as PersonalStatement FROM public.target_role_profiles WHERE user_id = @userId";
        return await conn.QueryAsync<TargetProfileDropdownDto>(sql, new { userId });
    }

    public async Task<object> GetProfileDetailsAsync(string profileId, string userId)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        var profile = await conn.QueryFirstOrDefaultAsync<dynamic>(
            @"SELECT job_role as ""jobRole"", personal_statement as ""personalStatement"", 
                     about_me as ""aboutMe"", portfolio_url as ""portfolioUrl"", cv_url as ""cvUrl"" 
              FROM public.target_role_profiles WHERE id = @p::uuid AND user_id = @u", 
            new { p = profileId, u = userId });

        if (profile == null) throw new Exception("Profile not found.");

        var skills = await conn.QueryAsync(@"SELECT skill_name as ""skillName"", level FROM public.skill WHERE profile_id = @p::uuid", new { p = profileId });
        var experience = await conn.QueryAsync(@"SELECT company_name as ""companyName"", to_char(start_date, 'YYYY-MM-DD') as ""startDate"", role_description as ""roleDescription"" FROM public.experience WHERE profile_id = @p::uuid", new { p = profileId });

        return new {
            JobRole = profile.jobRole ?? "",
            PersonalStatement = profile.personalStatement ?? "",
            AboutMe = profile.aboutMe ?? "",
            PortfolioUrl = profile.portfolioUrl ?? "",
            CvUrl = profile.cvUrl ?? "",
            Skills = skills,
            Experience = experience
        };
    }

    // ✅ NEW: Fetches the frozen snapshot data for a specific application
    public async Task<object> GetApplicationByIdAsync(string appId, string userId)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        // 1. Get the core application, user, and main snapshot info
var mainData = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT 
                a.id::text as ""appId"",
                to_char(a.applied_date, 'YYYY-MM-DD') as ""appliedDate"",
                a.status as ""status"",
                u.full_name as ""fullName"",
                u.email as ""email"",
                u.phone as ""phone"",
                u.address as ""address"",
                u.profile_image_url as ""profileImageUrl"",  /* ✅ ADDED THIS LINE */
                s.id::text as ""snapshotId"",
                s.job_role as ""jobRole"",
                s.personal_statement as ""personalStatement"",
                s.about_me as ""aboutMe"",
                s.cv_url as ""cvUrl"",
                s.match_score as ""matchScore"",
                s.industry_score as ""industryScore""
            FROM public.job_applications a
            JOIN public.""user"" u ON a.user_id = u.id
            JOIN public.application_snapshots s ON a.snapshot_id = s.id
            WHERE a.id = @appId::uuid AND a.user_id = @userId
        ", new { appId, userId });

        if (mainData == null) throw new Exception("Application not found or unauthorized.");

        string snapId = mainData.snapshotId;

        // 2. Fetch the frozen arrays linked to the snapshot
        var skills = await conn.QueryAsync(@"
            SELECT skill_name as ""skillName"", level 
            FROM public.snapshot_skills WHERE snapshot_id = @snapId::uuid", new { snapId });

        var experience = await conn.QueryAsync(@"
            SELECT company_name as ""companyName"", to_char(start_date, 'YYYY-MM-DD') as ""startDate"", 
                   to_char(end_date, 'YYYY-MM-DD') as ""endDate"", role_description as ""roleDescription"" 
            FROM public.snapshot_experience WHERE snapshot_id = @snapId::uuid", new { snapId });

        var education = await conn.QueryAsync(@"
            SELECT degree_title as ""degreeTitle"", field_of_study as ""fieldOfStudy"", 
                   organization as ""organization"", to_char(start_date, 'YYYY') as ""year"" 
            FROM public.snapshot_education WHERE snapshot_id = @snapId::uuid", new { snapId });

        // 3. Construct the JSON structure the frontend expects
        return new {
            id = mainData.appId,
            appliedDate = mainData.appliedDate,
            status = mainData.status,
            user = new {
                fullName = mainData.fullName,
                email = mainData.email,
                phone = mainData.phone,
                address = mainData.address,
                profileImageUrl = mainData.profileImageUrl
            },
            snapshot = new {
                jobRole = mainData.jobRole,
                personalStatement = mainData.personalStatement,
                aboutMe = mainData.aboutMe,
                cvUrl = mainData.cvUrl,
                matchScore = mainData.matchScore,
                industryScore = mainData.industryScore,
                skills = skills,
                experience = experience,
                education = education
            }
        };
    }

    public async Task<bool> SubmitApplicationAsync(string userId, ApplyForJobDto dto)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        using var trans = await conn.BeginTransactionAsync();

        try 
        {
            Guid snapshotId = Guid.NewGuid();

            var jobSkills = await conn.QueryAsync<dynamic>(
                "SELECT skill_name, required_level FROM public.job_skills WHERE job_id = @jobId::uuid",
                new { jobId = dto.JobId }
            );

            var userSkills = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(dto.SkillsJson) ?? new();

            int GetSkillWeight(string level) => level?.ToLower() switch {
                "expert" => 100,
                "intermediate" => 40,
                "beginner" => 10,
                _ => 0
            };

            double totalRequiredPoints = 0;
            double earnedPoints = 0;

            foreach (var js in jobSkills)
            {
                string jsName = js.skill_name?.ToString() ?? "";
                string jsLevel = js.required_level?.ToString() ?? "";
                int reqWeight = GetSkillWeight(jsLevel);

                totalRequiredPoints += reqWeight;

                // Find if user submitted this skill (case-insensitive)
                var uSkill = userSkills.FirstOrDefault(s => 
                    s.TryGetValue("skillName", out var n) && 
                    n.Equals(jsName, StringComparison.OrdinalIgnoreCase));

                int userWeight = 0;
                if (uSkill != null && uSkill.TryGetValue("level", out var uLevel))
                {
                    userWeight = GetSkillWeight(uLevel);
                }

                // Cap the earned points
                earnedPoints += Math.Min(userWeight, reqWeight);
            }

            int calculatedCompanyScore = totalRequiredPoints > 0 
                ? (int)Math.Round((earnedPoints / totalRequiredPoints) * 100) 
                : 0;

            // =========================================================================
            // 1. Insert Main Application Snapshot Data (Including the calculated score)
            // =========================================================================
            string snapSql = @"
                INSERT INTO public.application_snapshots 
                (id, job_role, portfolio_url, personal_statement, about_me, cv_url, match_score, industry_score, company_skill_match_score, created_at)
                VALUES 
                (@snapId, @jobRole, @portfolioUrl, @personalStatement, @aboutMe, @cvUrl, @matchScore, @industryScore, @companyMatchScore, CURRENT_TIMESTAMP);
            ";

            await conn.ExecuteAsync(snapSql, new { 
                snapId = snapshotId, 
                jobRole = dto.JobRole, 
                portfolioUrl = dto.PortfolioUrl,
                personalStatement = dto.PersonalStatement, 
                aboutMe = dto.AboutMe, 
                cvUrl = dto.CvUrl,
                matchScore = dto.MatchScore, 
                industryScore = dto.IndustryScore,
                companyMatchScore = calculatedCompanyScore // ✅ Saved from backend calculation
            }, trans);

            // 2. Insert Frontend-Edited Skills
            if (userSkills.Count > 0) {
                foreach(var s in userSkills) {
                    await conn.ExecuteAsync(@"INSERT INTO public.snapshot_skills (snapshot_id, skill_name, level) VALUES (@snapId, @name, @lvl)", 
                    new { snapId = snapshotId, name = s["skillName"], lvl = s["level"] }, trans);
                }
            }

            // 3. Insert Frontend-Edited Experience
            var experience = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(dto.ExperienceJson);
            if (experience != null) {
                foreach(var e in experience) {
                    string safeDate = string.IsNullOrWhiteSpace(e["startDate"]) ? "1900-01-01" : e["startDate"];
                    await conn.ExecuteAsync(@"INSERT INTO public.snapshot_experience (snapshot_id, company_name, start_date, role_description) VALUES (@snapId, @company, @start::date, @desc)", 
                    new { snapId = snapshotId, company = e["companyName"], start = safeDate, desc = e["roleDescription"] }, trans);
                }
            }

            // =========================================================================
            // 4. CLONE THE REST OF THE PROFILE DIRECTLY FROM THE DATABASE
            // =========================================================================
            if (!string.IsNullOrEmpty(dto.ProfileId))
            {
                var pId = new { snapId = snapshotId, profileId = dto.ProfileId };

                await conn.ExecuteAsync(@"INSERT INTO public.snapshot_social_links (snapshot_id, platform_name, profile_url)
                    SELECT @snapId, platform_name, profile_url FROM public.social_link WHERE profile_id = @profileId::uuid", pId, trans);

                await conn.ExecuteAsync(@"INSERT INTO public.snapshot_education (snapshot_id, degree_title, field_of_study, organization, start_date, end_date, honors, thesis_title, relevant_coursework)
                    SELECT @snapId, degree_title, field_of_study, organization, start_date, end_date, honors, thesis_title, relevant_coursework FROM public.education WHERE profile_id = @profileId::uuid", pId, trans);

                await conn.ExecuteAsync(@"INSERT INTO public.snapshot_projects (snapshot_id, name, description, time_period, role, organization, source_link)
                    SELECT @snapId, name, description, time_period, role, organization, source_link FROM public.project WHERE profile_id = @profileId::uuid", pId, trans);

                await conn.ExecuteAsync(@"INSERT INTO public.snapshot_publications (snapshot_id, title, description, source_link, organization, year)
                    SELECT @snapId, title, description, source_link, organization, year FROM public.publication WHERE profile_id = @profileId::uuid", pId, trans);

                await conn.ExecuteAsync(@"INSERT INTO public.snapshot_certifications (snapshot_id, organization, field, issue_date)
                    SELECT @snapId, organization, field, issue_date FROM public.certification WHERE profile_id = @profileId::uuid", pId, trans);

                await conn.ExecuteAsync(@"INSERT INTO public.snapshot_memberships (snapshot_id, organization_name)
                    SELECT @snapId, organization_name FROM public.membership WHERE profile_id = @profileId::uuid", pId, trans);

                await conn.ExecuteAsync(@"INSERT INTO public.snapshot_languages (snapshot_id, language_name, proficiency)
                    SELECT @snapId, language_name, proficiency FROM public.language WHERE profile_id = @profileId::uuid", pId, trans);

                await conn.ExecuteAsync(@"INSERT INTO public.snapshot_teaching_experience (snapshot_id, courses_taught, organization, time_period, curriculum_description)
                    SELECT @snapId, courses_taught, organization, time_period, curriculum_description FROM public.teaching_experience WHERE profile_id = @profileId::uuid", pId, trans);

                await conn.ExecuteAsync(@"INSERT INTO public.snapshot_awards (snapshot_id, award_name, organization, description)
                    SELECT @snapId, award_name, organization, description FROM public.award WHERE profile_id = @profileId::uuid", pId, trans);

                await conn.ExecuteAsync(@"INSERT INTO public.snapshot_volunteers (snapshot_id, organization, role, description)
                    SELECT @snapId, organization, role, description FROM public.volunteer WHERE profile_id = @profileId::uuid", pId, trans);

                await conn.ExecuteAsync(@"
                    INSERT INTO public.snapshot_research_experience (snapshot_id, project_name, organization, results_description, lab_or_field_work, linked_publication_title)
                    SELECT @snapId, r.project_name, r.organization, r.results_description, r.lab_or_field_work, p.title 
                    FROM public.research_experience r
                    LEFT JOIN public.publication p ON r.linked_publication_id = p.id
                    WHERE r.profile_id = @profileId::uuid", pId, trans);
            }

            // 5. Create Job Application Record
            string appSql = @"
                INSERT INTO public.job_applications 
                (id, user_id, snapshot_id, job_id, cover_letter, applied_date, status)
                VALUES 
                (@appId, @userId, @snapId, @jobId::uuid, @coverLetter, CURRENT_TIMESTAMP, 'Pending');
            ";
            await conn.ExecuteAsync(appSql, new { 
                appId = Guid.NewGuid(), userId = userId, snapId = snapshotId, 
                jobId = dto.JobId, coverLetter = dto.CoverLetter
            }, trans);

            // 6. Update user statistics
            await conn.ExecuteAsync("UPDATE public.\"user\" SET applied_jobs = COALESCE(applied_jobs, 0) + 1 WHERE id = @userId", new { userId }, trans);

            await trans.CommitAsync();
            return true;
        } 
        catch (Exception ex)
        {
            await trans.RollbackAsync();
            throw new Exception("Database Error: " + ex.Message);
        }
    }
}
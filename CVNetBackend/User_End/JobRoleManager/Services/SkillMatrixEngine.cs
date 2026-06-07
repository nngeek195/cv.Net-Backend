using System.Data;
using Dapper;
using Npgsql;
using CVNetBackend.JobRoleManager.Models;

namespace CVNetBackend.JobRoleManager.Services;

public class SkillMatrixEngine
{
    private readonly string _connectionString;

    public SkillMatrixEngine(IConfiguration configuration)
    {
        string host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        string port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        string db = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres"; 
        string user = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        string pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";
        
        _connectionString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};";
    }

    private double GetLevelPercentage(string level)
    {
        return level.ToLower().Trim() switch {
            "beginner" => 8.5, "intermediate" => 34.0, "expert" => 85.0, _ => 0.0
        };
    }

    public async Task<ReadinessReport> CalculateUserReadinessAsync(string userId, string? profileId = null)
    {
        using IDbConnection db = new NpgsqlConnection(_connectionString);

        // ✅ FIX: Pure snake_case
        const string userProfileQuery = @"
            SELECT p.id as profile_id, p.job_role, c.name as category_name, c.skills as category_skills, c.id as category_id
            FROM public.target_role_profiles p
            LEFT JOIN (
                SELECT DISTINCT job_role, job_category_id 
                FROM public.general_skills
            ) g ON g.job_role = p.job_role
            LEFT JOIN public.job_categories c ON c.id = g.job_category_id
            JOIN public.""user"" u ON p.user_id = u.id
            WHERE p.user_id = @UserId AND (p.id = @ProfileId::uuid OR @ProfileId IS NULL)
            LIMIT 1;";

        var profileMeta = await db.QueryFirstOrDefaultAsync<dynamic>(userProfileQuery, new { UserId = userId, ProfileId = profileId });

        if (profileMeta == null) return new ReadinessReport { JobRole = "General", JobCategory = "General", UserReadinessScore = 0 };

        string activeRole = (string)(profileMeta.job_role ?? "General");
        string categoryName = (string)(profileMeta.category_name ?? "General");
        string[] categoryBaselineSkills = profileMeta.category_skills is string[] arr ? arr : Array.Empty<string>();

        const string generalSkillsQuery = "SELECT skill_name, level FROM public.general_skills WHERE LOWER(job_role) = LOWER(@JobRole);";
        var roleSpecificSkills = (await db.QueryAsync<dynamic>(generalSkillsQuery, new { JobRole = activeRole })).ToList();

        var profileUuid = (Guid)profileMeta.profile_id;
        
        const string userSkillsQuery = @"SELECT skill_name as skillName, level FROM public.skill WHERE profile_id = @ProfileId::uuid;";
        var userClaimedSkills = (await db.QueryAsync<dynamic>(userSkillsQuery, new { ProfileId = profileUuid }))
            .ToDictionary(k => ((string)k.skillname).ToLower().Trim(), v => (string)v.level);

        var report = new ReadinessReport { JobRole = activeRole, JobCategory = categoryName };
        double totalIndustryPoints = 0; double totalUserPoints = 0;
        var evaluatedTracker = new HashSet<string>();

        foreach (var skill in categoryBaselineSkills.Concat(roleSpecificSkills.Select(s => (string)s.skill_name)))
        {
            if (string.IsNullOrEmpty(skill) || !evaluatedTracker.Add(skill.ToLower())) continue;
            
            double expectedPercent = 34.0;
            var specMatch = roleSpecificSkills.FirstOrDefault(s => ((string)s.skill_name).ToLower() == skill.ToLower());
            if (specMatch != null) expectedPercent = GetLevelPercentage(specMatch.level);

            double userPercent = 0.0;
            string userLevel = "Missing";
            if (userClaimedSkills.TryGetValue(skill.ToLower(), out var claimed)) {
                userLevel = claimed;
                userPercent = Math.Min(GetLevelPercentage(claimed), expectedPercent);
            }

            totalIndustryPoints += expectedPercent;
            totalUserPoints += userPercent;

            report.Breakdown.Add(new SkillCalculationDetail {
                SkillName = skill, RequirementSource = specMatch != null ? "Role Specific" : "Category Baseline",
                ExpectedLevel = specMatch != null ? specMatch.level : "Intermediate", ExpectedPercentage = expectedPercent,
                UserDeclaredLevel = userLevel, UserCalculatedPercentage = userPercent
            });
        }

        if (report.Breakdown.Count > 0) {
            report.IndustryTargetBenchmark = Math.Round(totalIndustryPoints / report.Breakdown.Count, 2);
            report.UserReadinessScore = Math.Round(totalUserPoints / report.Breakdown.Count, 2);
        }
        return report;
    }
}
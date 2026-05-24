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
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    private double GetLevelPercentage(string level)
    {
        return level.ToLower().Trim() switch
        {
            "beginner" => 8.5,
            "intermediate" => 34.0,
            "expert" => 85.0,
            _ => 0.0
        };
    }

    public async Task<ReadinessReport> CalculateUserReadinessAsync(string userId)
    {
        using IDbConnection db = new NpgsqlConnection(_connectionString);

        // 1. Fetch user's registered job role and the parent category data
        const string userProfileQuery = @"
            SELECT u.id, u.job_role, c.name as category_name, c.skills as category_skills, c.id as category_id
            FROM public.""user"" u
            JOIN public.job_categories c ON u.job_category_id = c.id
            WHERE u.id = @UserId LIMIT 1;";

        var profileMeta = await db.QueryFirstOrDefaultAsync<dynamic>(userProfileQuery, new { UserId = userId });

        // ✅ Strong explicit validation guards clear out all CS8602 warnings safely
        if (profileMeta == null || profileMeta.job_role == null || profileMeta.category_id == null)
            throw new Exception("User profile details or assigned industry track relations are unassigned.");

        string activeRole = (string)profileMeta.job_role;
        string categoryName = (string)(profileMeta.category_name ?? "General");
        Guid categoryId = (Guid)profileMeta.category_id;
        string[] categoryBaselineSkills = profileMeta.category_skills ?? Array.Empty<string>();

        // 2. Fetch all role-specific requirements out of GeneralSkill
        const string generalSkillsQuery = @"
            SELECT skill_name, level 
            FROM public.general_skills 
            WHERE LOWER(job_role) = LOWER(@JobRole) AND job_category_id = @CategoryId;";
        
        var roleSpecificSkills = (await db.QueryAsync<dynamic>(generalSkillsQuery, new { JobRole = activeRole, CategoryId = categoryId })).ToList();

        // 3. Fetch user's personal current claimed skills from singular table layout
        const string userSkillsQuery = @"
            SELECT skill_name, level 
            FROM public.skill 
            WHERE user_id = @UserId;";
        
        var userClaimedSkills = (await db.QueryAsync<dynamic>(userSkillsQuery, new { UserId = userId }))
            .ToDictionary(k => ((string)k.skill_name).ToLower().Trim(), v => (string)v.level);

        var report = new ReadinessReport
        {
            JobRole = activeRole,
            JobCategory = categoryName
        };

        double totalIndustryPoints = 0;
        double totalUserPoints = 0;
        var evaluatedSkillsTracker = new HashSet<string>();

        // --- LAYER 1: Process Category Common Baseline Skills (Defaulted to Intermediate) ---
        foreach (var skill in categoryBaselineSkills)
        {
            string cleanSkill = skill.Trim();
            if (string.IsNullOrEmpty(cleanSkill) || !evaluatedSkillsTracker.Add(cleanSkill.ToLower())) continue;

            double expectedPercent = 34.0; 
            double userPercent = 0.0;
            string userLevel = "Missing";

            if (userClaimedSkills.TryGetValue(cleanSkill.ToLower(), out var claimedLevel))
            {
                userLevel = claimedLevel;
                double rawUserPercent = GetLevelPercentage(claimedLevel);
                userPercent = Math.Min(rawUserPercent, expectedPercent);
            }

            totalIndustryPoints += expectedPercent;
            totalUserPoints += userPercent;

            report.Breakdown.Add(new SkillCalculationDetail
            {
                SkillName = cleanSkill,
                RequirementSource = "Category Baseline",
                ExpectedLevel = "Intermediate",
                ExpectedPercentage = expectedPercent,
                UserDeclaredLevel = userLevel,
                UserCalculatedPercentage = userPercent
            });
        }

        // --- LAYER 2: Process Specialized Role-Specific Skills ---
        foreach (var specSkill in roleSpecificSkills)
        {
            if (specSkill.skill_name == null) continue;
            string skillName = ((string)specSkill.skill_name).Trim();
            if (!evaluatedSkillsTracker.Add(skillName.ToLower())) continue; 

            string expectedLevel = specSkill.level ?? "Intermediate";
            double expectedPercent = GetLevelPercentage(expectedLevel);
            double userPercent = 0.0;
            string userLevel = "Missing";

            if (userClaimedSkills.TryGetValue(skillName.ToLower(), out var claimedLevel))
            {
                userLevel = claimedLevel;
                double rawUserPercent = GetLevelPercentage(claimedLevel);
                userPercent = Math.Min(rawUserPercent, expectedPercent);
            }

            totalIndustryPoints += expectedPercent;
            totalUserPoints += userPercent;

            report.Breakdown.Add(new SkillCalculationDetail
            {
                SkillName = skillName,
                RequirementSource = "Role Specific",
                ExpectedLevel = expectedLevel,
                ExpectedPercentage = expectedPercent,
                UserDeclaredLevel = userLevel,
                UserCalculatedPercentage = userPercent
            });
        }

        // --- LAYER 3: Aggregate Averages ---
        int totalUniqueSkills = report.Breakdown.Count;
        if (totalUniqueSkills > 0)
        {
            report.IndustryTargetBenchmark = Math.Round(totalIndustryPoints / totalUniqueSkills, 2);
            report.UserReadinessScore = Math.Round(totalUserPoints / totalUniqueSkills, 2);
        }

        return report;
    }
}
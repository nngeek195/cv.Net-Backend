using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Npgsql;
using Dapper;

namespace CVNetBackend.Controllers.SkillGap;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SkillGapController : ControllerBase
{
    private readonly string _connString;

    public SkillGapController(IConfiguration config)
    {
        string host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        string port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        string db = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres"; 
        string user = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        string pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";
        _connString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};";
    }

    [HttpGet("analysis")]
    public async Task<IActionResult> GetSkillGapAnalysis([FromQuery] string? profileId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync();

            var profiles = (await conn.QueryAsync<dynamic>(
                "SELECT id::text, job_role as \"jobRole\" FROM public.target_role_profiles WHERE user_id = @uid AND job_role != 'General CV Profile'",
                new { uid = userId })).ToList();

            if (!profiles.Any()) return Ok(new { profiles = new List<object>(), activeProfileId = "", matchScore = 0, industryScore = 0, matchedCount = 0, missingCount = 0, matchedSkills = new List<string>(), missingSkills = new List<string>(), breakdown = new List<object>() });

            string activeId = string.IsNullOrEmpty(profileId) ? profiles.First().id : profileId;
            var activeProfile = profiles.FirstOrDefault(p => p.id == activeId) ?? profiles.First();
            string jobRole = activeProfile.jobRole;

            var userSkills = (await conn.QueryAsync<dynamic>(
                "SELECT skill_name, level FROM public.skill WHERE profile_id = @pid::uuid", 
                new { pid = activeId })).ToList();

            var coreSkills = (await conn.QueryAsync<dynamic>(
                "SELECT skill_name, level, job_category_id FROM public.general_skills WHERE LOWER(job_role) = LOWER(@role) ORDER BY created_at ASC", 
                new { role = jobRole })).ToList();

            var categoryId = coreSkills.FirstOrDefault()?.job_category_id;
            var categorySkills = new List<string>();
            if (categoryId != null)
            {
                var category = await conn.QueryFirstOrDefaultAsync<dynamic>("SELECT skills FROM public.job_categories WHERE id = @cid::uuid", new { cid = categoryId });
                if (category != null && category.skills != null)
                {
                    var usedCore = coreSkills.Select(x => x.skill_name.ToString().ToLower()).ToHashSet();
                    categorySkills = ((string[])category.skills).Where(x => !usedCore.Contains(x.ToLower())).ToList();
                }
            }

            var breakdownList = new List<object>();
            var matchedSkillsList = new List<string>();
            var missingSkillsList = new List<string>();

            // =========================================================
            // ✅ THE EXACT MATH ENGINE YOU REQUESTED
            // Formula: (x*34 + n*8.5 + m*34 + t*85) / (x+n+m+t)
            // =========================================================
            double ToWeight(string? lvl) => lvl?.ToLower() switch { "expert" => 85, "advanced" => 85, "intermediate" => 34, "beginner" => 8.5, _ => 0 };
            
            double expectedSum = 0;
            double userSum = 0;
            int totalCount = 0;
            
            int matchedCoreCount = 0;
            int missingCoreCount = 0;
            int matchedCategoryCount = 0;
            int missingCategoryCount = 0;

            // 1. Process Core Skills (n, m, t)
            foreach (var cs in coreSkills)
            {
                string skillName = cs.skill_name.ToString();
                string expectedLevel = cs.level.ToString();
                double expW = ToWeight(expectedLevel);

                var uSkill = userSkills.FirstOrDefault(x => x.skill_name.ToString().ToLower() == skillName.ToLower());
                string userLevel = uSkill?.level?.ToString() ?? "Missing";
                double usrW = ToWeight(userLevel);

                // ✅ The Cap Rule: if user_current > expect_level => expect_level = current_level
                if (usrW > expW) usrW = expW; 

                expectedSum += expW;
                userSum += usrW;
                totalCount++;

                if (usrW >= expW) 
                {
                    matchedSkillsList.Add(skillName);
                    matchedCoreCount++;
                }
                else 
                {
                    missingSkillsList.Add(skillName);
                    missingCoreCount++;
                }

                breakdownList.Add(new {
                    skill = skillName,
                    category = "Core Role Requirement",
                    yourLevel = userLevel == "Missing" ? "None Detected" : userLevel,
                    required = expectedLevel
                });
            }

            // 2. Process JobCategory Skills block (x = 1 block of other skills)
            if (categorySkills.Count > 0)
            {
                bool hasAll = true;
                bool hasAny = false;

                foreach (var catSkill in categorySkills)
                {
                    var uSkill = userSkills.FirstOrDefault(x => x.skill_name.ToString().ToLower() == catSkill.ToLower());
                    if (uSkill != null) 
                    {
                        // Add individual hit to matched list!
                        matchedSkillsList.Add(catSkill);
                        hasAny = true;
                    }
                    else 
                    {
                        hasAll = false;
                    }
                }

                totalCount++; // x = 1
                double expW = 34; // Intermediate block expectation
                expectedSum += expW;

                if (hasAll) 
                {
                    matchedCategoryCount = 1;
                    userSum += 34; // They fulfilled the entire block
                }
                else 
                {
                    missingCategoryCount = 1;
                    missingSkillsList.Add("Other Category Skills"); // Group missing skills together
                    userSum += hasAny ? 8.5 : 0; // If they have some, give beginner weight (8.5)
                }

                breakdownList.Add(new {
                    skill = "Other Category Skills",
                    category = "Job Category Requirement",
                    yourLevel = hasAll ? "Intermediate" : (hasAny ? "Beginner" : "None Detected"),
                    required = "Intermediate"
                });
            }

            // 3. Final Absolute Mathematics out of Max 85!
            int industryScore = totalCount > 0 ? (int)Math.Round(expectedSum / totalCount) : 80;
            int matchScore = totalCount > 0 ? (int)Math.Round(userSum / totalCount) : 0;

            int matchedCountUI = matchedCoreCount + matchedCategoryCount;
            int missingCountUI = missingCoreCount + missingCategoryCount;

            return Ok(new
            {
                profiles,
                activeProfileId = activeId,
                jobRole,
                matchScore, // This now maxes out perfectly at 85!
                industryScore, // This is exactly (x*34 + n*8.5 + m*34 + t*85)/(x+n+m+t)
                matchedCount = matchedCountUI, // Perfect Core + 1 logic
                missingCount = missingCountUI,
                matchedSkills = matchedSkillsList,
                missingSkills = missingSkillsList,
                breakdown = breakdownList
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SKILL GAP ERROR] {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
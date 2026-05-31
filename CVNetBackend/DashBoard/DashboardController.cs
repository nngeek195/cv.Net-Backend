using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CVNetBackend.Services;
using System.Security.Claims;
using Npgsql;
using Dapper;

namespace CVNetBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboardService;
    private readonly IConfiguration _config;
    private readonly string _connString;

    public DashboardController(DashboardService dashboardService, IConfiguration config)
    {
        _dashboardService = dashboardService;
        _config = config;

        string host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        string port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        string db = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres";
        string user = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        string pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";

        _connString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};";
    }

    [HttpDelete("roles/{profileId}")]
    public async Task<IActionResult> DeleteRole(string profileId, [FromQuery] bool force = false)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { error = "Invalid user token." });

            var result = await _dashboardService.TryDeleteProfileAsync(userId, profileId, force);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("available-tracks")]
    public async Task<IActionResult> GetAvailableTracks()
    {
        try
        {
            return Ok(await _dashboardService.GetAvailableJobTracksAsync());
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] string? profileId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var data = await _dashboardService.GetDashboardDataAsync(userId, profileId);
            return Ok(data);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("roles")]
    public async Task<IActionResult> AddRole([FromBody] System.Text.Json.JsonElement body)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            string jobRole = body.GetProperty("jobRole").GetString() ?? "";
            string category = body.GetProperty("category").GetString() ?? "";

            var success = await _dashboardService.AddTargetRoleProfileAsync(userId, jobRole, category);
            return success ? Ok(new { message = "Role added successfully." }) : BadRequest("Failed to map track layout.");
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ==========================================================
    // ✅ ADVANCED READINESS MATRIX (SYNCED WITH SKILL GAP LOGIC)
    // ==========================================================
    [HttpGet("readiness-matrix")]
    public async Task<IActionResult> GetReadinessMatrix([FromQuery] string profileId)
    {
        try
        {
            using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync();

            var profile = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT job_role FROM public.target_role_profiles WHERE id = @pid::uuid", 
                new { pid = profileId });

            if (profile == null) return Ok(new { breakdown = new List<object>(), matchScore = 0, industryScore = 80 });

            string role = profile.job_role?.ToString() ?? "";

            var userSkills = (await conn.QueryAsync<dynamic>(
                "SELECT skill_name, level FROM public.skill WHERE profile_id = @pid::uuid", 
                new { pid = profileId })).ToList();

            var coreSkills = (await conn.QueryAsync<dynamic>(
                "SELECT skill_name, level, job_category_id FROM public.general_skills WHERE LOWER(job_role)=LOWER(@role) ORDER BY created_at ASC", 
                new { role })).ToList();

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

            var breakdown = new List<object>();

            // --- THE EXACT MATHEMATICAL FORMULA ---
            double ToWeight(string? lvl) => lvl?.ToLower() switch { "expert" => 85, "advanced" => 85, "intermediate" => 34, "beginner" => 8.5, _ => 0 };
            
            double expectedSum = 0;
            double userSum = 0;
            int totalCount = 0;

            // 1. Core Skills
            foreach (var cs in coreSkills)
            {
                string skillName = cs.skill_name.ToString();
                string expectedLevel = cs.level.ToString();
                double expW = ToWeight(expectedLevel);

                var uSkill = userSkills.FirstOrDefault(x => x.skill_name.ToString().ToLower() == skillName.ToLower());
                string userLevel = uSkill?.level?.ToString() ?? "Missing";
                double usrW = ToWeight(userLevel);

                if (usrW > expW) usrW = expW; // Apply Cap

                expectedSum += expW;
                userSum += usrW;
                totalCount++;

                breakdown.Add(new {
                    skillName = skillName,
                    requirementSource = "Core Role Skill",
                    expectedLevel = expectedLevel,
                    userDeclaredLevel = userLevel == "Missing" ? "None Detected" : userLevel
                });
            }

            // 2. JobCategory Skills
            if (categorySkills.Count > 0)
            {
                bool hasAll = true;
                bool hasAny = false;

                foreach (var catSkill in categorySkills)
                {
                    var uSkill = userSkills.FirstOrDefault(x => x.skill_name.ToString().ToLower() == catSkill.ToLower());
                    if (uSkill != null) hasAny = true;
                    else hasAll = false;
                }

                totalCount++;
                double expW = 34; // Intermediate target
                expectedSum += expW;

                string otherUserLevel = "None Detected";
                if (hasAll) { otherUserLevel = "Intermediate"; userSum += 34; }
                else if (hasAny) { otherUserLevel = "Beginner"; userSum += 8.5; }

                breakdown.Add(new {
                    skillName = "Other Category Skills",
                    requirementSource = "Category Skill",
                    expectedLevel = "Intermediate",
                    userDeclaredLevel = otherUserLevel
                });
            }

            int matchScore = totalCount > 0 ? (int)Math.Round(userSum / totalCount) : 0;
            int industryScore = totalCount > 0 ? (int)Math.Round(expectedSum / totalCount) : 80;

            return Ok(new { breakdown, matchScore, industryScore });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[READINESS MATRIX ERROR] {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }
}
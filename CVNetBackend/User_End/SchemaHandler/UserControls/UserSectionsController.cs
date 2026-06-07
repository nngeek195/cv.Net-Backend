using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Npgsql;
using Dapper;

namespace CVNetBackend.SchemaHandler.UserControls;

[ApiController]
[Route("api/UserProfile")]
public class UserProfileController : ControllerBase
{
    private readonly string _connString;

    public UserProfileController(IConfiguration config)
    {
        string host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        string port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        string db = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres"; 
        string user = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        string pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";
        _connString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};";
    }

    [HttpGet("full-profile")]
    public async Task<IActionResult> GetFullProfile([FromQuery] string userId)
    {
        try 
        {
            using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync();

            var userRow = await conn.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT u.id, u.full_name as ""fullName"", u.email, u.phone, u.address, u.gpa, u.employment_status as ""employmentStatus"",
                         t.id as valid_profile_id
                  FROM public.""user"" u
                  LEFT JOIN public.target_role_profiles t ON u.default_profile_id = t.id
                  WHERE u.id = @u", new { u = userId });
            
            if (userRow == null) 
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO public.""user"" (id, full_name, email, employment_status, created_at, updated_at) 
                    VALUES (@uid, '', '', 'Unemployed', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                    ON CONFLICT (id) DO NOTHING;", new { uid = userId });

                userRow = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT u.id, u.full_name as ""fullName"", u.email, u.phone, u.address, u.gpa, u.employment_status as ""employmentStatus"",
                             t.id as valid_profile_id
                      FROM public.""user"" u
                      LEFT JOIN public.target_role_profiles t ON u.default_profile_id = t.id
                      WHERE u.id = @u", new { u = userId });
            }

            string profileId = userRow.valid_profile_id?.ToString();

            var masterProfile = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT id::text FROM public.target_role_profiles WHERE user_id = @u AND job_role = 'General CV Profile'", 
                new { u = userId });

            if (string.IsNullOrEmpty(masterProfile))
            {
                masterProfile = Guid.NewGuid().ToString();
                await conn.ExecuteAsync(@"
                    INSERT INTO public.target_role_profiles (id, user_id, job_role, created_at, updated_at) 
                    VALUES (@pid::uuid, @uid, 'General CV Profile', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
                ", new { pid = masterProfile, uid = userId });
            }

            if (string.IsNullOrEmpty(profileId)) 
            {
                profileId = masterProfile;
                await conn.ExecuteAsync("UPDATE public.\"user\" SET default_profile_id = @pid::uuid WHERE id = @uid", new { pid = profileId, uid = userId });
            }

            var result = new Dictionary<string, object>
            {
                { "activeProfileId", profileId },
                { "masterProfileId", masterProfile },
                { "fullName", userRow.fullName ?? "" },
                { "email", userRow.email ?? "" },
                { "phone", userRow.phone ?? "" },
                { "address", userRow.address ?? "" },
                { "gpa", userRow.gpa?.ToString() ?? "" },
                { "employmentStatus", userRow.employmentStatus ?? "" }
            };

            var availableProfiles = await conn.QueryAsync(
                @"SELECT id::text as id, job_role as ""jobRole"", 
                         CASE WHEN job_role = 'General CV Profile' THEN true ELSE false END as ""isMaster""
                  FROM public.target_role_profiles WHERE user_id = @u ORDER BY created_at ASC", 
                new { u = userId });
            result["availableProfiles"] = availableProfiles;

            var profileRow = await conn.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT job_role as ""jobRole"", portfolio_url as ""portfolioUrl"", current_org as ""currentOrg"", 
                         current_position as ""currentPosition"", personal_statement as ""personalStatement"", about_me as ""aboutMe"", cv_url as ""cvUrl""
                  FROM public.target_role_profiles WHERE id = @p::uuid", new { p = profileId });

            if (profileRow != null)
            {
                result["jobRole"] = profileRow.jobRole ?? "";
                result["portfolioUrl"] = profileRow.portfolioUrl ?? "";
                result["currentOrg"] = profileRow.currentOrg ?? "";
                result["currentPosition"] = profileRow.currentPosition ?? "";
                result["personalStatement"] = profileRow.personalStatement ?? "";
                result["aboutMe"] = profileRow.aboutMe ?? "";
                result["cvUrl"] = profileRow.cvUrl ?? "";
            }

            result["skills"] = await conn.QueryAsync(@"SELECT id::text, skill_name as ""skillName"", level FROM public.skill WHERE profile_id = @p::uuid", new { p = profileId });
            result["experience"] = await conn.QueryAsync(@"SELECT id::text, company_name as ""companyName"", to_char(start_date, 'YYYY-MM-DD') as ""startDate"", to_char(end_date, 'YYYY-MM-DD') as ""endDate"", role_description as ""roleDescription"" FROM public.experience WHERE profile_id = @p::uuid", new { p = profileId });
            result["education"] = await conn.QueryAsync(@"SELECT id::text, degree_title as ""degreeTitle"", field_of_study as ""fieldOfStudy"", organization, to_char(start_date, 'YYYY-MM-DD') as ""startDate"", to_char(end_date, 'YYYY-MM-DD') as ""endDate"", honors, thesis_title as ""thesisTitle"" FROM public.education WHERE profile_id = @p::uuid", new { p = profileId });
            result["certifications"] = await conn.QueryAsync(@"SELECT id::text, organization, field, to_char(issue_date, 'YYYY-MM-DD') as ""issueDate"" FROM public.certification WHERE profile_id = @p::uuid", new { p = profileId });
            result["languages"] = await conn.QueryAsync(@"SELECT id::text, language_name as ""languageName"", proficiency FROM public.language WHERE profile_id = @p::uuid", new { p = profileId });
            result["projects"] = await conn.QueryAsync(@"SELECT id::text, name, description, time_period as ""timePeriod"", role, organization, source_link as ""sourceLink"" FROM public.project WHERE profile_id = @p::uuid", new { p = profileId });
            result["publications"] = await conn.QueryAsync(@"SELECT id::text, title, description, source_link as ""sourceLink"", organization, year FROM public.publication WHERE profile_id = @p::uuid", new { p = profileId });
            result["teachingExperience"] = await conn.QueryAsync(@"SELECT id::text, courses_taught as ""coursesTaught"", organization, time_period as ""timePeriod"", curriculum_description as ""curriculumDescription"" FROM public.teaching_experience WHERE profile_id = @p::uuid", new { p = profileId });
            result["researchExperience"] = await conn.QueryAsync(@"SELECT id::text, project_name as ""projectName"", lab_or_field_work as ""labOrFieldWork"", organization, results_description as ""resultsDescription"" FROM public.research_experience WHERE profile_id = @p::uuid", new { p = profileId });
            result["awards"] = await conn.QueryAsync(@"SELECT id::text, award_name as ""awardName"", organization, description FROM public.award WHERE profile_id = @p::uuid", new { p = profileId });
            result["volunteer"] = await conn.QueryAsync(@"SELECT id::text, organization, role, description FROM public.volunteer WHERE profile_id = @p::uuid", new { p = profileId });
            result["memberships"] = await conn.QueryAsync(@"SELECT id::text, organization_name as ""organizationName"" FROM public.membership WHERE profile_id = @p::uuid", new { p = profileId });
            result["socialLinks"] = await conn.QueryAsync(@"SELECT id::text, platform_name as ""platformName"", profile_url as ""profileUrl"" FROM public.social_link WHERE profile_id = @p::uuid", new { p = profileId });

            return Ok(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GET FULL PROFILE ERROR] {ex.Message}");
            return StatusCode(500, new { error = "Database crash", details = ex.Message });
        }
    }

    [HttpPost("switch-profile")]
    public async Task<IActionResult> SwitchProfile([FromBody] SwitchProfileDto data)
    {
        try 
        {
            using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync();
            await conn.ExecuteAsync("UPDATE public.\"user\" SET default_profile_id = @pid::uuid, updated_at = CURRENT_TIMESTAMP WHERE id = @uid", new { pid = data.ProfileId, uid = data.UserId });
            return Ok(new { success = true });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpPost("clone-profile")]
    public async Task<IActionResult> CloneProfile([FromBody] CloneProfileDto data)
    {
        try 
        {
            using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync();

            await conn.ExecuteAsync(@"
                UPDATE public.target_role_profiles t
                SET 
                    portfolio_url = COALESCE(NULLIF(t.portfolio_url, ''), m.portfolio_url), 
                    current_org = COALESCE(NULLIF(t.current_org, ''), m.current_org), 
                    current_position = COALESCE(NULLIF(t.current_position, ''), m.current_position), 
                    personal_statement = COALESCE(NULLIF(t.personal_statement, ''), m.personal_statement), 
                    about_me = COALESCE(NULLIF(t.about_me, ''), m.about_me), 
                    cv_url = COALESCE(NULLIF(t.cv_url, ''), m.cv_url)
                FROM public.target_role_profiles m 
                WHERE t.id = @tp::uuid AND m.id = @mp::uuid", 
                new { tp = data.TargetProfileId, mp = data.MasterProfileId });

            string[] queries = {
                @"INSERT INTO public.skill (profile_id, skill_name, level, created_at, updated_at) 
                  SELECT @tp::uuid, m.skill_name, m.level, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP 
                  FROM public.skill m WHERE m.profile_id = @mp::uuid 
                  AND NOT EXISTS (SELECT 1 FROM public.skill t WHERE t.profile_id = @tp::uuid AND LOWER(t.skill_name) = LOWER(m.skill_name))",
                
                @"INSERT INTO public.experience (profile_id, company_name, start_date, end_date, role_description, created_at, updated_at) 
                  SELECT @tp::uuid, m.company_name, m.start_date, m.end_date, m.role_description, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP 
                  FROM public.experience m WHERE m.profile_id = @mp::uuid
                  AND NOT EXISTS (SELECT 1 FROM public.experience t WHERE t.profile_id = @tp::uuid AND LOWER(t.company_name) = LOWER(m.company_name) AND LOWER(t.role_description) = LOWER(m.role_description))",
                
                @"INSERT INTO public.education (profile_id, degree_title, field_of_study, organization, start_date, end_date, honors, thesis_title, relevant_coursework, created_at, updated_at) 
                  SELECT @tp::uuid, m.degree_title, m.field_of_study, m.organization, m.start_date, m.end_date, m.honors, m.thesis_title, m.relevant_coursework, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP 
                  FROM public.education m WHERE m.profile_id = @mp::uuid
                  AND NOT EXISTS (SELECT 1 FROM public.education t WHERE t.profile_id = @tp::uuid AND LOWER(t.degree_title) = LOWER(m.degree_title) AND LOWER(t.organization) = LOWER(m.organization))",
                
                @"INSERT INTO public.social_link (profile_id, platform_name, profile_url, created_at, updated_at) 
                  SELECT @tp::uuid, m.platform_name, m.profile_url, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP 
                  FROM public.social_link m WHERE m.profile_id = @mp::uuid
                  AND NOT EXISTS (SELECT 1 FROM public.social_link t WHERE t.profile_id = @tp::uuid AND LOWER(t.platform_name) = LOWER(m.platform_name))"
            };

            foreach (var q in queries) {
                await conn.ExecuteAsync(q, new { tp = data.TargetProfileId, mp = data.MasterProfileId });
            }

            return Ok(new { success = true });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpPut("profile-update")]
    public async Task<IActionResult> UpdateProfileField([FromBody] FieldUpdateDto data)
    {
        try 
        {
            using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync();

            string[] userFields = { "fullName", "phone", "address", "gpa" };
            string[] profileFields = { "jobRole", "portfolioUrl", "currentOrg", "currentPosition", "personalStatement", "aboutMe" };

            if (userFields.Contains(data.Field))
            {
                string dbField = data.Field switch { "gpa" => "gpa", "fullName" => "full_name", _ => data.Field };
                object val = DBNull.Value;
                if (!string.IsNullOrEmpty(data.Value)) { if (data.Field == "gpa") { if (float.TryParse(data.Value, out float g)) val = g; } else { val = data.Value; } }
                await conn.ExecuteAsync($"UPDATE public.\"user\" SET {dbField} = @v, updated_at = CURRENT_TIMESTAMP WHERE id = @uid", new { v = val, uid = data.UserId });
                return Ok();
            }

            if (profileFields.Contains(data.Field))
            {
                string dbField = data.Field switch { "jobRole" => "job_role", "portfolioUrl" => "portfolio_url", "currentOrg" => "current_org", "currentPosition" => "current_position", "personalStatement" => "personal_statement", "aboutMe" => "about_me", _ => data.Field };
                await conn.ExecuteAsync($"UPDATE public.target_role_profiles SET {dbField} = @v, updated_at = CURRENT_TIMESTAMP WHERE id = @pid::uuid", new { v = data.Value, pid = data.ProfileId });
                return Ok();
            }

            return BadRequest("Field not mapped.");
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpPost("collection/{tableName}")]
    public async Task<IActionResult> AddCollectionItem(string tableName, [FromBody] JsonElement payload)
    {
        try 
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload.GetRawText());
            if (dict == null || !dict.ContainsKey("profileId")) return BadRequest("Missing Profile ID.");

            string profileIdStr = dict["profileId"].ValueKind == JsonValueKind.Null ? "" : dict["profileId"].ToString();
            dict.Remove("profileId"); dict.Remove("id"); dict.Remove("profile_id"); dict.Remove("created_at"); dict.Remove("updated_at");

            var columns = new List<string> { "profile_id", "created_at", "updated_at" };
            var values = new List<string> { "@profileId::uuid", "CURRENT_TIMESTAMP", "CURRENT_TIMESTAMP" };
            var parameters = new DynamicParameters();
            
            if(!Guid.TryParse(profileIdStr, out Guid parsedGuid)) return BadRequest("Invalid Profile ID");
            parameters.Add("profileId", parsedGuid);

            foreach (var kvp in dict)
            {
                string val = kvp.Value.ValueKind == JsonValueKind.Null ? null : kvp.Value.ToString();
                
                columns.Add($"\"{kvp.Key}\""); 

                // =========================================================================
                // ✅ SMART TYPE CASTING ENGINE
                // Forces PostgreSQL to natively identify the payload format without crashing!
                // =========================================================================
                if (kvp.Key.Contains("date"))
                {
                    // Forces `::date` on the SQL value so PostgreSQL converts the text instantly.
                    values.Add($"@{kvp.Key}::date");
                    parameters.Add(kvp.Key, DateTime.TryParse(val, out DateTime dt) ? dt.ToString("yyyy-MM-dd") : "1900-01-01");
                }
                else if (kvp.Key == "year")
                {
                    // Forces `::integer` explicitly to map cleanly to your Int! schema types.
                    values.Add($"@{kvp.Key}::integer");
                    parameters.Add(kvp.Key, int.TryParse(val, out int y) ? y : 0);
                }
                else
                {
                    // Everything else is treated natively as text/string.
                    values.Add($"@{kvp.Key}::text");
                    parameters.Add(kvp.Key, string.IsNullOrWhiteSpace(val) ? "" : val);
                }
            }

            string sql = $"INSERT INTO public.\"{tableName.ToLower()}\" ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})";

            using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync();
            await conn.ExecuteAsync(sql, parameters);
            
            return Ok(new { success = true });
        } 
        catch (PostgresException ex) when (ex.SqlState == "23503") 
        { 
            return BadRequest("Foreign Key Error: Active Profile not found."); 
        }
        catch (Exception ex)
        {
            string errorDetails = ex.InnerException?.Message ?? ex.Message;
            Console.WriteLine($"[COLLECTION INSERT ERROR] {errorDetails}");
            return StatusCode(500, new { error = errorDetails });
        }
    }

    [HttpDelete("collection/{tableName}/{id}")]
    public async Task<IActionResult> DeleteCollectionItem(string tableName, string id)
    {
        try 
        {
            using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync();
            await conn.ExecuteAsync($"DELETE FROM public.\"{tableName.ToLower()}\" WHERE id = @id::uuid", new { id });
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class FieldUpdateDto { public string UserId { get; set; } = string.Empty; public string ProfileId { get; set; } = string.Empty; public string Field { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; }
public class SwitchProfileDto { public string UserId { get; set; } = string.Empty; public string ProfileId { get; set; } = string.Empty; }
public class CloneProfileDto { public string MasterProfileId { get; set; } = string.Empty; public string TargetProfileId { get; set; } = string.Empty; }
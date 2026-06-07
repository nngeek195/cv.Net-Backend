using Npgsql;
using Dapper;
using CVNetBackend.JobRoleManager.Services;

namespace CVNetBackend.Services;

public class DashboardSummaryDto
{
    public List<ProfileDto> Profiles { get; set; } = new();
    public string? ActiveProfileId { get; set; }
    public GlobalStatsDto GlobalStats { get; set; } = new();
    public ActiveProfileDataDto ActiveProfileData { get; set; } = new();
}

public class ProfileDto { public string Id { get; set; } = ""; public string JobRole { get; set; } = ""; }
public class GlobalStatsDto { public int Applied { get; set; } public int Requests { get; set; } public int Rejects { get; set; } }
public class ActiveProfileDataDto
{
    public int CompletenessPercentage { get; set; }
    public double SkillMatchPercentage { get; set; }
    public int RoleAppliedCount { get; set; }
    public List<RecentAppDto> RecentApps { get; set; } = new();
}
public class RecentAppDto { public string Role { get; set; } = ""; public string Company { get; set; } = ""; public string Date { get; set; } = ""; public string Status { get; set; } = ""; }

public class CategoryRolesDto { public string CategoryName { get; set; } = ""; public List<string> Roles { get; set; } = new(); }

public class DeletionResult
{
    public bool Success { get; set; }
    public bool IsBlocked { get; set; }
    public bool NeedsConfirmation { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class DashboardService
{
    private readonly string _connString;
    private readonly SkillMatrixEngine _skillEngine;

    public DashboardService(IConfiguration config, SkillMatrixEngine skillEngine)
    {
        string host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        string port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        string db = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres"; 
        string user = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        string pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";
        
        _connString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};";
        _skillEngine = skillEngine;
    }

    public async Task<List<CategoryRolesDto>> GetAvailableJobTracksAsync()
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT DISTINCT c.name as CategoryName, g.job_role as JobRole
            FROM public.job_categories c
            JOIN public.general_skills g ON c.id = g.job_category_id
            WHERE g.job_role IS NOT NULL;";

        var rawRows = await conn.QueryAsync<dynamic>(sql);
        
        return rawRows
            .GroupBy(r => (string)r.categoryname)
            .Select(g => new CategoryRolesDto
            {
                CategoryName = g.Key,
                Roles = g.Select(r => (string)r.jobrole).Distinct().ToList()
            }).ToList();
    }

    public async Task<bool> AddTargetRoleProfileAsync(string userId, string jobRole, string categoryName)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        var categoryId = await conn.ExecuteScalarAsync<Guid?>(
            "SELECT id FROM public.job_categories WHERE name = @name", new { name = categoryName });
        if (categoryId == null) return false;

        var profileId = Guid.NewGuid();
        
        await conn.ExecuteAsync(@"
            INSERT INTO public.target_role_profiles (id, user_id, job_role, created_at, updated_at)
            VALUES (@id, @u, @role, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);",
            new { id = profileId, u = userId, role = jobRole });

        await conn.ExecuteAsync(@"
            UPDATE public.""user"" 
            SET default_profile_id = COALESCE(default_profile_id, @id::uuid) 
            WHERE id = @u AND default_profile_id IS NULL;",
            new { id = profileId, u = userId });

        return true;
    }

    public async Task<DashboardSummaryDto> GetDashboardDataAsync(string userId, string? requestedProfileId = null)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        var result = new DashboardSummaryDto();

        var userRow = await conn.QueryFirstOrDefaultAsync<dynamic>(
            @"SELECT id, phone, address, gpa, profile_image_url, default_profile_id 
              FROM public.""user"" WHERE id = @u", new { u = userId });
        
        if (userRow == null) return result;

        result.GlobalStats = new GlobalStatsDto {
            Applied = await conn.ExecuteScalarAsync<int>(@"SELECT COUNT(1) FROM public.job_applications WHERE user_id = @u", new { u = userId }),
            Requests = await conn.ExecuteScalarAsync<int>(@"SELECT COUNT(1) FROM public.call_for_interviews WHERE user_id = @u", new { u = userId }),
            Rejects = await conn.ExecuteScalarAsync<int>(@"SELECT COUNT(1) FROM public.reject_records WHERE user_id = @u", new { u = userId })
        };

        var profiles = await conn.QueryAsync<ProfileDto>(
            @"SELECT id::text as Id, job_role as JobRole FROM public.target_role_profiles WHERE user_id = @u", new { u = userId });
        result.Profiles = profiles.ToList();

        result.ActiveProfileId = requestedProfileId ?? userRow.default_profile_id?.ToString() ?? result.Profiles.FirstOrDefault()?.Id;

        double filledSections = 0;
        double totalSections = 14;

        bool hasPhone = !string.IsNullOrEmpty((string?)userRow.phone);
        bool hasAddress = !string.IsNullOrEmpty((string?)userRow.address);
        bool hasGpa = userRow.gpa != null && userRow.gpa > 0;
        bool hasImage = !string.IsNullOrEmpty((string?)userRow.profile_image_url);
        if (hasPhone || hasAddress || hasGpa || hasImage) filledSections++;

        if (!string.IsNullOrEmpty(result.ActiveProfileId))
        {
            var profileRow = await conn.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT portfolio_url, current_org, current_position, personal_statement, about_me 
                  FROM public.target_role_profiles WHERE id = @p::uuid", new { p = result.ActiveProfileId });

            if (profileRow != null)
            {
                bool hasPortfolio = !string.IsNullOrEmpty((string?)profileRow.portfolio_url);
                bool hasOrg = !string.IsNullOrEmpty((string?)profileRow.current_org);
                bool hasPos = !string.IsNullOrEmpty((string?)profileRow.current_position);
                bool hasStmt = !string.IsNullOrEmpty((string?)profileRow.personal_statement);
                bool hasAbout = !string.IsNullOrEmpty((string?)profileRow.about_me);
                
                if (hasPortfolio || hasOrg || hasPos || hasStmt || hasAbout) filledSections++;
            }

            string[] subTables = { 
                "skill", "experience", "education", "project", "publication", 
                "certification", "membership", "language", "teaching_experience", 
                "research_experience", "award", "volunteer" 
            };

            foreach (var table in subTables) 
            {
                var count = await conn.ExecuteScalarAsync<int>($@"SELECT COUNT(1) FROM public.""{table}"" WHERE profile_id = @p::uuid", new { p = result.ActiveProfileId });
                if (count > 0) filledSections++;
            }
        }

        result.ActiveProfileData.CompletenessPercentage = (int)Math.Round((filledSections / totalSections) * 100);

        try 
        {
            var skillReport = await _skillEngine.CalculateUserReadinessAsync(userId, result.ActiveProfileId);
            result.ActiveProfileData.SkillMatchPercentage = skillReport.UserReadinessScore;
        } 
        catch { result.ActiveProfileData.SkillMatchPercentage = 0; }

        // ✅ FIX: Querying Job Applications by linking through the Snapshot Table
        if (!string.IsNullOrEmpty(result.ActiveProfileId))
        {
            result.ActiveProfileData.RoleAppliedCount = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(1) 
                FROM public.job_applications a
                JOIN public.application_snapshots s ON a.snapshot_id = s.id
                WHERE s.job_role = (SELECT job_role FROM public.target_role_profiles WHERE id = @p::uuid)
                AND a.user_id = @u", new { p = result.ActiveProfileId, u = userId });

            var apps = await conn.QueryAsync<RecentAppDto>(@"
                SELECT j.title as Role, c.name as Company, to_char(a.applied_date, 'Mon DD, YYYY') as Date, a.status as Status
                FROM public.job_applications a
                JOIN public.application_snapshots s ON a.snapshot_id = s.id
                JOIN public.jobs j ON a.job_id = j.id
                JOIN public.companies c ON j.company_id = c.id
                WHERE s.job_role = (SELECT job_role FROM public.target_role_profiles WHERE id = @p::uuid)
                AND a.user_id = @u
                ORDER BY a.applied_date DESC LIMIT 5", new { p = result.ActiveProfileId, u = userId });
            
            result.ActiveProfileData.RecentApps = apps.ToList();
        }

        return result;
    }

    public async Task<DeletionResult> TryDeleteProfileAsync(string userId, string profileId, bool isConfirmedByUser)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        // Get the role string to check the linked snapshots
        var roleName = await conn.ExecuteScalarAsync<string>("SELECT job_role FROM public.target_role_profiles WHERE id = @p::uuid", new { p = profileId });

        // ✅ FIX: All checks now correctly traverse through Snapshots and user_id
        var activeApps = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1) FROM public.job_applications a 
            JOIN public.application_snapshots s ON a.snapshot_id = s.id 
            WHERE s.job_role = @role AND a.status IN ('Pending', 'In Review') AND a.user_id = @u", 
            new { role = roleName, u = userId });

        var activeInterviews = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1) FROM public.call_for_interviews c
            JOIN public.job_applications a ON c.application_id = a.id
            JOIN public.application_snapshots s ON a.snapshot_id = s.id
            WHERE s.job_role = @role AND a.user_id = @u", 
            new { role = roleName, u = userId });

        if (activeApps > 0 || activeInterviews > 0)
            return new DeletionResult { IsBlocked = true, Message = "You cannot remove this role because an application is currently processing under this job role." };

        var hiredApps = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1) FROM public.hired_records h
            JOIN public.job_applications a ON h.application_id = a.id
            JOIN public.application_snapshots s ON a.snapshot_id = s.id
            WHERE s.job_role = @role AND a.user_id = @u", 
            new { role = roleName, u = userId });
            
        if (hiredApps > 0 && !isConfirmedByUser)
            return new DeletionResult { NeedsConfirmation = true, Message = "You have received job offers and achievements under this role. Do you really want to delete all these records?" };

        var hasData = await conn.ExecuteScalarAsync<int>(@"SELECT COUNT(1) FROM public.target_role_profiles WHERE id = @p::uuid AND (about_me IS NOT NULL OR portfolio_url IS NOT NULL)", new { p = profileId });
        
        if (hasData > 0 && !isConfirmedByUser)
            return new DeletionResult { NeedsConfirmation = true, Message = "If you remove this job role, all tailored CV data added under it will be lost. Proceed?" };

        using var trans = await conn.BeginTransactionAsync();
        try 
        {
            // ✅ FIX: Removed 'reject_records' and 'job_applications' from this array! 
            // Applications are tied to immutable snapshots. Deleting a profile should NOT delete application history!
            string[] tables = { "skill", "experience", "education", "project", "publication", "certification", "membership", "language", "teaching_experience", "research_experience", "award", "volunteer" };
            
            foreach (var table in tables) {
                await conn.ExecuteAsync($"DELETE FROM public.\"{table}\" WHERE profile_id = @p::uuid", new { p = profileId }, trans);
            }

            await conn.ExecuteAsync("DELETE FROM public.target_role_profiles WHERE id = @p::uuid", new { p = profileId }, trans);
            
            await trans.CommitAsync();
            return new DeletionResult { Success = true, Message = "Role removed successfully." };
        } 
        catch { await trans.RollbackAsync(); throw; }
    }
}
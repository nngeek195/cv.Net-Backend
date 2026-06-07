using Npgsql;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace CVNetBackend.Services;

public class UserService
{
    private readonly string _connString;

    public UserService(IConfiguration config)
    {
        // Explicitly build the connection to guarantee it is NEVER null
        string host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        string port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        string db = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres";
        string user = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        string pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";

        // Includes the SSL fix we applied earlier!
        _connString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};SslMode=Require;Trust Server Certificate=true;";
    }

    public async Task DeleteFullUserProfile(string uid)
    {
        // Instantiating the connection directly so it cannot be null
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        
        using var trans = await conn.BeginTransactionAsync();

        try 
        {
            string sql = @"
                -- 1. Unlink default profile to prevent foreign key loop crashes
                UPDATE public.""user"" SET default_profile_id = NULL WHERE id = @uid;

                -- 2. Wipe Job Tracking (These tables use user_id)
                DELETE FROM public.job_applications WHERE user_id = @uid;
                DELETE FROM public.hired_records WHERE user_id = @uid;
                DELETE FROM public.call_for_interviews WHERE user_id = @uid;
                DELETE FROM public.reject_records WHERE user_id = @uid;

                -- 3. Wipe CV Data Arrays (These tables use profile_id, NOT user_id)
                DELETE FROM public.skill WHERE profile_id IN (SELECT id FROM public.target_role_profiles WHERE user_id = @uid);
                DELETE FROM public.experience WHERE profile_id IN (SELECT id FROM public.target_role_profiles WHERE user_id = @uid);
                DELETE FROM public.education WHERE profile_id IN (SELECT id FROM public.target_role_profiles WHERE user_id = @uid);
                DELETE FROM public.social_link WHERE profile_id IN (SELECT id FROM public.target_role_profiles WHERE user_id = @uid);
                DELETE FROM public.project WHERE profile_id IN (SELECT id FROM public.target_role_profiles WHERE user_id = @uid);
                DELETE FROM public.publication WHERE profile_id IN (SELECT id FROM public.target_role_profiles WHERE user_id = @uid);
                DELETE FROM public.certification WHERE profile_id IN (SELECT id FROM public.target_role_profiles WHERE user_id = @uid);
                DELETE FROM public.membership WHERE profile_id IN (SELECT id FROM public.target_role_profiles WHERE user_id = @uid);
                DELETE FROM public.language WHERE profile_id IN (SELECT id FROM public.target_role_profiles WHERE user_id = @uid);
                DELETE FROM public.teaching_experience WHERE profile_id IN (SELECT id FROM public.target_role_profiles WHERE user_id = @uid);
                DELETE FROM public.research_experience WHERE profile_id IN (SELECT id FROM public.target_role_profiles WHERE user_id = @uid);
                DELETE FROM public.award WHERE profile_id IN (SELECT id FROM public.target_role_profiles WHERE user_id = @uid);
                DELETE FROM public.volunteer WHERE profile_id IN (SELECT id FROM public.target_role_profiles WHERE user_id = @uid);

                -- 4. Delete the Target Profiles
                DELETE FROM public.target_role_profiles WHERE user_id = @uid;

                -- 5. Delete Root User Account
                DELETE FROM public.""user"" WHERE id = @uid;
            ";

            using var cmd = new NpgsqlCommand(sql, conn, trans);
            cmd.Parameters.AddWithValue("uid", uid);
            
            await cmd.ExecuteNonQueryAsync();
            await trans.CommitAsync();
        } 
        catch (Exception ex)
        {
            await trans.RollbackAsync();
            Console.WriteLine($"[CRITICAL DELETE ERROR] {ex.Message}");
            throw;
        }
    }
}
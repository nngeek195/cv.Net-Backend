using Npgsql;

namespace CVNetBackend.Services;

public class UserService
{
    private readonly DatabaseService _db;
    public UserService(DatabaseService db) => _db = db;

    public async Task DeleteFullUserProfile(string uid)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var trans = await conn.BeginTransactionAsync();

        try {
            // ✅ FIX: Formatted exactly to match PostgreSQL snake_case table generation
            // ✅ NEW: Added the job tracking tables (job_applications, call_for_interviews, etc.)
            string[] profileTables = { 
                "social_link", 
                "skill", 
                "experience", 
                "education", 
                "project", 
                "publication", 
                "certification", 
                "membership", 
                "language", 
                "teaching_experience", 
                "research_experience", 
                "award", 
                "volunteer",
                "job_applications",
                "call_for_interviews",
                "reject_records",
                "hired_records"
            };

            foreach (var table in profileTables) {
                // Delete child records where the user_id matches
                var cmd = new NpgsqlCommand($"DELETE FROM public.\"{table}\" WHERE user_id = @uid", conn, trans);
                cmd.Parameters.AddWithValue("uid", uid);
                
                // We use a try-catch here just in case a table hasn't been synced to the DB yet,
                // so it doesn't crash the entire deletion process if a table is missing.
                try {
                    await cmd.ExecuteNonQueryAsync();
                } catch (PostgresException ex) when (ex.SqlState == "42P01") {
                    Console.WriteLine($"[WARNING] Table {table} does not exist yet. Skipping...");
                }
            }

            // Finally, delete the parent user record
            var userCmd = new NpgsqlCommand("DELETE FROM public.\"user\" WHERE id = @uid", conn, trans);
            userCmd.Parameters.AddWithValue("uid", uid);
            await userCmd.ExecuteNonQueryAsync();

            await trans.CommitAsync();
        } catch {
            await trans.RollbackAsync();
            throw;
        }
    }
}
using Npgsql;

namespace CVNetBackend.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService()
    {
        // Pulling from your new 5-variable .env structure
        string host = Environment.GetEnvironmentVariable("DB_HOST");
        string user = Environment.GetEnvironmentVariable("DB_USER");
        string password = Environment.GetEnvironmentVariable("DB_PASSWORD");
        string database = Environment.GetEnvironmentVariable("DB_NAME");
        string port = Environment.GetEnvironmentVariable("DB_PORT");

        // Building the official Npgsql connection string
        _connectionString = $"Host={host};Port={port};Username={user};Password={password};Database={database};";
    }

    public async Task SaveToPostgres(string uid, string firstName, string lastName, string email)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"
            INSERT INTO public.""user"" 
            (id, email, full_name, employment_status, created_at, updated_at) 
            VALUES 
            (@id, @email, @fullName, @employmentStatus, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)";
        
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", uid);
        cmd.Parameters.AddWithValue("email", email);
        cmd.Parameters.AddWithValue("fullName", $"{firstName} {lastName}");
        cmd.Parameters.AddWithValue("employmentStatus", "Unspecified"); 
        
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpsertUserToPostgres(string uid, string email, string fullName)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"
            INSERT INTO public.""user"" (id, email, full_name, employment_status, created_at, updated_at)
            VALUES (@id, @email, @fullName, 'Unspecified', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            ON CONFLICT (id) 
            DO UPDATE SET 
                email = EXCLUDED.email, 
                full_name = EXCLUDED.full_name, 
                updated_at = CURRENT_TIMESTAMP"; 

        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", uid);
        cmd.Parameters.AddWithValue("email", email);
        cmd.Parameters.AddWithValue("fullName", fullName);

        await cmd.ExecuteNonQueryAsync();
    }
    // Profile Update
    public async Task UpdateProfileImage(string uid, string imageUrl)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // SQL uses snake_case for column names by convention
        var sql = @"UPDATE public.""user"" SET profile_image_url = @url, updated_at = CURRENT_TIMESTAMP WHERE id = @id";

        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("url", imageUrl);
        cmd.Parameters.AddWithValue("id", uid);

        await cmd.ExecuteNonQueryAsync();
    }
}
using Npgsql;
using Microsoft.Extensions.Configuration; 

namespace CVNetBackend.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IConfiguration configuration)
    {
        // 1. Read the credentials directly from the loaded .env file
        var host = Environment.GetEnvironmentVariable("DB_HOST");
        var port = Environment.GetEnvironmentVariable("DB_PORT");
        var user = Environment.GetEnvironmentVariable("DB_USER");
        var pass = Environment.GetEnvironmentVariable("DB_PASSWORD");
        var db = Environment.GetEnvironmentVariable("DB_NAME");

        // 2. Build the string securely in memory
        if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
        {
            _connectionString = $"Host={host};Port={port};Username={user};Password={pass};Database={db};";
        }
        else
        {
            // Fallback in case the .env fails to load
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("Environment variables or DefaultConnection string are completely missing.");
        }
    }

    public NpgsqlConnection GetConnection() => new NpgsqlConnection(_connectionString);

    public async Task UpsertUserToPostgres(string uid, string email, string fullName, string agreement)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // ✅ FIX: Reverted to the correct snake_case column names your database actually uses
        var sql = @"
            INSERT INTO public.""user"" (id, email, full_name, employment_status, agreement, created_at, updated_at)
            VALUES (@id, @email, @fullName, 'Unspecified', @agreement, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            ON CONFLICT (id) 
            DO UPDATE SET 
                email = EXCLUDED.email, 
                full_name = EXCLUDED.full_name, 
                agreement = EXCLUDED.agreement,
                updated_at = CURRENT_TIMESTAMP;"; 

        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", uid);
        cmd.Parameters.AddWithValue("email", email);
        cmd.Parameters.AddWithValue("fullName", fullName);
        cmd.Parameters.AddWithValue("agreement", agreement ?? "Agreed");

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateProfileImage(string uid, string imageUrl)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"UPDATE public.""user"" SET ""profileImageUrl"" = @url, ""updatedAt"" = CURRENT_TIMESTAMP WHERE id = @id";

        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("url", imageUrl);
        cmd.Parameters.AddWithValue("id", uid);

        await cmd.ExecuteNonQueryAsync();
    }
}
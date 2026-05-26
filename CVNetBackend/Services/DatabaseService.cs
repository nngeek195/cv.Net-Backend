using Npgsql;
using Microsoft.Extensions.Configuration; 

namespace CVNetBackend.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IConfiguration configuration)
    {
        var host = Environment.GetEnvironmentVariable("DB_HOST");
        var port = Environment.GetEnvironmentVariable("DB_PORT");
        var user = Environment.GetEnvironmentVariable("DB_USER");
        var pass = Environment.GetEnvironmentVariable("DB_PASSWORD");
        var db = Environment.GetEnvironmentVariable("DB_NAME");

        if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
        {
            _connectionString = $"Host={host};Port={port};Username={user};Password={pass};Database={db};";
        }
        else
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("Environment variables or DefaultConnection string are missing.");
        }
    }

    public NpgsqlConnection GetConnection() => new NpgsqlConnection(_connectionString);

    // ✅ FIX: Accepts firstName and lastName individually
    // ✅ FIX: Back to accepting a single 'fullName' to match the SQL column 'full_name'
    public async Task UpsertUserToPostgres(string uid, string email, string fullName, string agreement)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

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

        var sql = @"UPDATE public.""user"" SET profile_image_url = @url, updated_at = CURRENT_TIMESTAMP WHERE id = @id";

        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("url", imageUrl);
        cmd.Parameters.AddWithValue("id", uid);

        await cmd.ExecuteNonQueryAsync();
    }

    // ✅ FIX: Now targets first_name and last_name during Settings Page updates
    public async Task<bool> UpdateUserDetails(string uid, string email, string fullName)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"UPDATE public.""user"" SET email = @email, full_name = @fullName, updated_at = CURRENT_TIMESTAMP WHERE id = @id";

        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("email", email);
        cmd.Parameters.AddWithValue("fullName", fullName);
        cmd.Parameters.AddWithValue("id", uid);

        // Returns true if the database was actually updated
        int rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }
}
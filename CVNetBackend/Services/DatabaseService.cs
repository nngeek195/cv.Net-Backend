using Npgsql;

namespace CVNetBackend.Services;

public class DatabaseService
{
    private readonly string _connString = "Host=YOUR_IP;Username=postgres;Password=CV.Net2026@capstone;Database=cvnet2026-capstone-2-database";

    public async Task SaveToPostgres(string uid, string firstName, string lastName, string email)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        // We use the singular "user" because that's what we found in your Cloud Shell
        var sql = "INSERT INTO public.\"user\" (id, email) VALUES (@id, @email)";
        
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", uid);
        cmd.Parameters.AddWithValue("email", email);
        
        await cmd.ExecuteNonQueryAsync();
    }
}
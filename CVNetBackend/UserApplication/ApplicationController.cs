using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Npgsql;
using Dapper;

namespace CVNetBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ApplicationController : ControllerBase
{
    private readonly string _connString;

    public ApplicationController(IConfiguration config)
    {
        string host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        string port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        string db = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres"; 
        string user = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        string pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";
        _connString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};";
    }

    [HttpGet("my-applications")]
    public async Task<IActionResult> GetMyApplications()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) 
            {
                return Unauthorized(new { error = "Invalid user token." });
            }

            using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync();

            // Joins the Application -> Job -> Company to get all the data your UI needs
            var applications = await conn.QueryAsync<dynamic>(@"
                SELECT 
                    ja.id::text as ""id"",
                    j.title as ""role"",
                    c.name as ""company"",
                    j.location as ""location"",
                    to_char(ja.applied_date, 'Mon DD, YYYY') as ""date"",
                    COALESCE(ja.status, 'Pending') as ""status""
                FROM public.job_applications ja
                INNER JOIN public.jobs j ON ja.job_id = j.id
                INNER JOIN public.companies c ON j.company_id = c.id
                WHERE ja.user_id = @uid
                ORDER BY ja.applied_date DESC
            ", new { uid = userId });

            return Ok(applications);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[APPLICATION FETCH ERROR] {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
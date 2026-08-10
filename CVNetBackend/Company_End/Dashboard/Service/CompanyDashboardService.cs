using Npgsql;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using CVNetBackend.Company_End.Models;

namespace CVNetBackend.Company_End.Services;

public class DashboardStatsResult
{
    public int TotalApps { get; set; }
    public int AvgMatch { get; set; }
}

public class CompanyDashboardService
{
    private readonly string _connString;

    public CompanyDashboardService(IConfiguration config)
    {
        string host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        string port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        string db = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres";
        string user = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        string pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";
        _connString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};SslMode=Require;Trust Server Certificate=true;";
    }

    public async Task<RecruiterDashboardDto> GetDashboardDataAsync(string hrEmail)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        var dashboard = new RecruiterDashboardDto();

        Console.WriteLine("-> Executing Query 1: Finding Company ID...");
        var companyId = await conn.QueryFirstOrDefaultAsync<Guid?>(
            "SELECT id FROM public.companies WHERE hr_email = @email LIMIT 1", 
            new { email = hrEmail });

        if (companyId == null) 
            throw new Exception($"Company profile not found for email: {hrEmail}. Did you log in with an account that hasn't created a company profile yet?");
        
        Console.WriteLine($"   SUCCESS: Found Company ID: {companyId}");

        Console.WriteLine("-> Executing Query 2: Fetching Open Positions...");
        dashboard.OpenPositions = await conn.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(id)::int FROM public.jobs WHERE company_id = @cid AND status = 1", 
            new { cid = companyId });

        Console.WriteLine("-> Executing Query 3: Fetching Total Apps & Avg Match...");
        var statsSql = @"
            SELECT 
                COUNT(ja.id)::int as TotalApps,
                COALESCE(ROUND(AVG(s.industry_score)), 0)::int as AvgMatch
            FROM public.job_applications ja
            JOIN public.jobs j ON ja.job_id = j.id
            JOIN public.application_snapshots s ON ja.snapshot_id = s.id
            WHERE j.company_id = @cid;
        ";
        var stats = await conn.QueryFirstOrDefaultAsync<DashboardStatsResult>(statsSql, new { cid = companyId });
        if (stats != null) {
            dashboard.TotalApplications = stats.TotalApps;
            dashboard.AverageMatchScore = stats.AvgMatch;
        }

        Console.WriteLine("-> Executing Query 4: Fetching Application Trends...");
        var trendsSql = @"
            WITH months AS (
                SELECT generate_series(
                    date_trunc('month', CURRENT_DATE - INTERVAL '5 months'),
                    date_trunc('month', CURRENT_DATE),
                    '1 month'::interval
                ) as month_date
            )
            SELECT 
                TO_CHAR(m.month_date, 'Mon') as Month,
                COUNT(ja.id)::int as Count
            FROM months m
            LEFT JOIN (
                SELECT ja.id, ja.applied_date 
                FROM public.job_applications ja
                JOIN public.jobs j ON ja.job_id = j.id
                WHERE j.company_id = @cid
            ) ja ON date_trunc('month', ja.applied_date) = m.month_date
            GROUP BY m.month_date
            ORDER BY m.month_date;
        ";
        var trends = await conn.QueryAsync<MonthlyTrendDto>(trendsSql, new { cid = companyId });
        dashboard.ApplicationTrends = trends.ToList();

        Console.WriteLine("-> Executing Query 5: Fetching Top Candidates...");
        var topCandidatesSql = @"
            SELECT 
                u.full_name as Name,
                u.email as Email,
                j.title as Role,
                s.industry_score as MatchScore,
                COALESCE(ja.status, 'Pending') as Stage
            FROM public.job_applications ja
            JOIN public.jobs j ON ja.job_id = j.id
            JOIN public.""user"" u ON ja.user_id = u.id
            JOIN public.application_snapshots s ON ja.snapshot_id = s.id
            WHERE j.company_id = @cid AND ja.status NOT IN ('Hired', 'Rejected')
            ORDER BY s.industry_score DESC
            LIMIT 5;
        ";

        var candidates = await conn.QueryAsync<TopCandidateDto>(topCandidatesSql, new { cid = companyId });
        dashboard.TopCandidates = candidates.ToList();

        return dashboard;
    }
}
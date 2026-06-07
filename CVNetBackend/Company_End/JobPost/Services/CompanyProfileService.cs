using Npgsql;
using Dapper;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using CVNetBackend.Company_End.Models;

namespace CVNetBackend.Company_End.Services;

public class CompanyProfileService
{
    private readonly string _connString;
    private readonly Cloudinary _cloudinary;

    public CompanyProfileService(IConfiguration config)
    {
        string host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        string port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        string db = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres";
        string user = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        string pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";
        _connString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};SslMode=Require;Trust Server Certificate=true;";

        string cloudName = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME") ?? "";
        string apiKey = Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY") ?? "";
        string apiSecret = Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET") ?? "";
        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account) { Api = { Secure = true } };
    }

    public async Task<CompanyProfileResponseDto?> GetProfileAsync(string email)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        string sql = @"
            SELECT 
                id::text, name, logo_url as LogoUrl, description, site_link as SiteLink, 
                hr_email as HrEmail, hr_contact_phone as HrContactPhone, employee_count::text as EmployeeCount
            FROM public.companies
            WHERE hr_email = @email LIMIT 1;
        ";

        return await conn.QueryFirstOrDefaultAsync<CompanyProfileResponseDto>(sql, new { email });
    }

    public async Task<bool> UpdateProfileAsync(string email, UpdateCompanyDto dto)
    {
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        string sql = @"
            UPDATE public.companies
            SET 
                name = @name, 
                description = @desc, 
                site_link = @siteLink, 
                hr_contact_phone = @phone, 
                employee_count = @empCount::employee_count_range
            WHERE hr_email = @email;
        ";

        int rows = await conn.ExecuteAsync(sql, new {
            name = dto.Name,
            desc = dto.Description,
            siteLink = dto.SiteLink,
            phone = dto.HrContactPhone,
            empCount = dto.EmployeeCount,
            email = email
        });

        return rows > 0;
    }

    public async Task<string> UploadLogoAsync(string email, IFormFile file)
    {
        if (file == null || file.Length == 0) throw new Exception("No file provided.");
        if (file.Length > 5 * 1024 * 1024) throw new Exception("File size exceeds 5MB limit.");

        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        var companyId = await conn.QueryFirstOrDefaultAsync<string>("SELECT id::text FROM public.companies WHERE hr_email = @email", new { email });
        if (companyId == null) throw new Exception("Company not found.");

        using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams()
        {
            File = new FileDescription(file.FileName, stream),
            Folder = "company_logos", 
            PublicId = companyId,     
            Overwrite = true,
            Transformation = new Transformation().Width(500).Height(500).Crop("fit")
        };

        var result = await _cloudinary.UploadAsync(uploadParams);
        if (result.Error != null) throw new Exception($"Cloudinary Upload Failed: {result.Error.Message}");

        string logoUrl = result.SecureUrl.ToString();
        await conn.ExecuteAsync("UPDATE public.companies SET logo_url = @url WHERE hr_email = @email", new { url = logoUrl, email });

        return logoUrl;
    }
}
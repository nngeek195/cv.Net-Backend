using Npgsql;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Google.Cloud.Firestore;
using Google.Apis.Auth.OAuth2;
using Dapper;

namespace CVNetBackend.Services;

public class AdminService
{
    private readonly string _connString;
    private readonly UserService _userService;
    private readonly FirestoreDb _firestoreDb;

    public AdminService(IConfiguration config, UserService userService)
    {
        _userService = userService;
        
        string host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        string port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        string db = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres";
        string user = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        string pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";
        _connString = $"Host={host};Port={port};Database={db};Username={user};Password={pass};SslMode=Require;Trust Server Certificate=true;";

        // ✅ FIX: Explicitly load the JSON key so Google doesn't crash looking for default credentials
        string keyPath = Path.Combine(Directory.GetCurrentDirectory(), "firebase-key.json");
        var credential = GoogleCredential.FromFile(keyPath);
        string projectId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT") ?? "cvnet2026-capstone";
        
        _firestoreDb = new FirestoreDbBuilder
        {
            ProjectId = projectId,
            Credential = credential
        }.Build();
    }

    public async Task SwitchCandidateToCompanyAsync(string uid, string fullName, string email)
    {
        // 1. Wipe Candidate Data via UserService
        await _userService.DeleteFullUserProfile(uid);

        // 2. Insert into PostgreSQL with a Guaranteed Unique Name
        using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        // Ensures uniqueness to prevent violating the @unique constraint
        string safeUidSegment = string.IsNullOrEmpty(uid) ? Guid.NewGuid().ToString().Substring(0, 6) : uid.Substring(0, 6);
        string uniqueCompanyName = string.IsNullOrWhiteSpace(fullName) ? $"Company_{safeUidSegment}" : $"{fullName} ({safeUidSegment})";
        string newCompanyId = Guid.NewGuid().ToString(); // Generating valid 36-char PostgreSQL UUID

        string companySql = @"
            INSERT INTO public.companies (id, name, hr_email, employee_count, created_at) 
            VALUES (@companyId::uuid, @name, @email, 'SOLO', CURRENT_TIMESTAMP)
            ON CONFLICT (name) DO NOTHING;
        ";

        await conn.ExecuteAsync(companySql, new 
        { 
            companyId = newCompanyId, 
            name = uniqueCompanyName, 
            email = email 
        });

        // 3. Update the Role in Firestore
        var docRef = _firestoreDb.Collection("users").Document(uid);
        await docRef.SetAsync(new { role = "company" }, SetOptions.MergeAll);
    }
}
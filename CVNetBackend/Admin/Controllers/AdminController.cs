using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CVNetBackend.Services;
using Google.Cloud.Firestore;
using Google.Apis.Auth.OAuth2;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace CVNetBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly AdminService _adminService;
    private readonly FirestoreDb _firestoreDb;

    public AdminController(AdminService adminService)
    {
        _adminService = adminService;
        
        // ✅ FIX: Explicitly load the JSON key here too
        string keyPath = Path.Combine(Directory.GetCurrentDirectory(), "firebase-key.json");
        var credential = GoogleCredential.FromFile(keyPath);
        string projectId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT") ?? "cvnet2026-capstone";
        
        _firestoreDb = new FirestoreDbBuilder
        {
            ProjectId = projectId,
            Credential = credential
        }.Build();
    }

    // List all users for the Admin panel
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        try
        {
            var snapshot = await _firestoreDb.Collection("users").GetSnapshotAsync();
            var users = snapshot.Documents.Select(doc => new {
                uid = doc.Id,
                email = doc.ContainsField("email") ? doc.GetValue<string>("email") : "",
                firstName = doc.ContainsField("firstName") ? doc.GetValue<string>("firstName") : "",
                lastName = doc.ContainsField("lastName") ? doc.GetValue<string>("lastName") : "",
                role = doc.ContainsField("role") ? doc.GetValue<string>("role") : "candidate"
            }).ToList();

            return Ok(users);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // Execute Role Change
    [HttpPost("make-company")]
    public async Task<IActionResult> MakeCompany([FromBody] RoleChangeDto req)
    {
        try
        {
            string fullName = $"{req.FirstName} {req.LastName}".Trim();
            await _adminService.SwitchCandidateToCompanyAsync(req.Uid, fullName, req.Email);
            
            return Ok(new { message = "Successfully converted user to a Company." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class RoleChangeDto
{
    public string Uid { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
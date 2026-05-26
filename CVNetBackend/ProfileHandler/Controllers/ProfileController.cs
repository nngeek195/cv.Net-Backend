using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using CVNetBackend.ProfileHandler;
using CVNetBackend.Services;

namespace CVNetBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] 
public class ProfileController : ControllerBase
{
    private readonly ProfileService _profileService;
    private readonly DatabaseService _db;
    private readonly FirestoreService _fs;
    
    public ProfileController(ProfileService profileService, DatabaseService db, FirestoreService fs)
    {
        _profileService = profileService;
        _db = db;
        _fs = fs;
    }

    [HttpPost("upload-image")]
    public async Task<IActionResult> UploadImage(IFormFile file) 
    {
        try 
        {
            var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(uid)) return Unauthorized("Invalid security token.");

            var url = await _profileService.UploadProfileImageAsync(uid, file);
            return Ok(new { status = "success", imageUrl = url });
        }
        catch (Exception ex)
        {
            return BadRequest(new { status = "error", message = ex.Message });
        }
    }

    [HttpPut("update-details")]
    public async Task<IActionResult> UpdateDetails([FromBody] UpdateProfileRequest req)
    {
        try 
        {
            var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(uid)) return Unauthorized();

            // 1. Split the name for Firestore (firstName and lastName)
            var parts = req.FullName.Trim().Split(' ');
            string firstName = parts[0];
            string lastName = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "";
            
            await _fs.UpsertUserDocument(uid, firstName, lastName, req.Email);

            // 2. Keep the name combined for PostgreSQL (full_name)
            bool success = await _db.UpdateUserDetails(uid, req.Email, req.FullName);

            if (!success) 
            {
                Console.WriteLine($"[SQL WARNING] 0 rows updated in Postgres for UID: {uid}");
            }

            return Ok(new { status = "success", message = "Profile updated." });
        }
        catch(Exception ex)
        {
            Console.WriteLine($"[CRITICAL ERROR] {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class UpdateProfileRequest 
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
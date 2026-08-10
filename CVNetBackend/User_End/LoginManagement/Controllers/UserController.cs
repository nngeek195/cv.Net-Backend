using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FirebaseAdmin.Auth;
using CVNetBackend.Services;
using System.Security.Claims;

namespace CVNetBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly UserService _userService;
    private readonly FirestoreService _firestoreService;

    public UserController(UserService userService, FirestoreService firestoreService)
    {
        _userService = userService;
        _firestoreService = firestoreService;
    }

    [HttpPost("initialize")]
    public async Task<IActionResult> InitializeUser()
    {
        var authHeader = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return Unauthorized("Missing or invalid token");

        string idToken = authHeader.Substring(7); 

        try {
            FirebaseToken decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(idToken);
            string uid = decodedToken.Uid; 
            Console.WriteLine($"✅ Verified User: {uid}");
            return Ok(new { userId = uid, message = "Identity Verified!" });
        }
        catch {
            return Unauthorized("Token verification failed");
        }
    }

    [HttpDelete("delete-account")]
    [Authorize]
    public async Task<IActionResult> DeleteAccount()
    {
        try
        {
            var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(uid)) return Unauthorized();

            Console.WriteLine($"[CRITICAL] Initiating full system wipe for UID: {uid}");

            // Delete the SQL profile data first.
            await _userService.DeleteFullUserProfile(uid);

            // Delete the Firestore document next.
            await _firestoreService.DeleteUserDocument(uid);

            // Remove the Firebase Auth identity last.
            await FirebaseAuth.DefaultInstance.DeleteUserAsync(uid);

            Console.WriteLine($"[SUCCESS] Data obliterated for UID: {uid}");
            return Ok(new { message = "Account successfully wiped from all systems." });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CRITICAL DELETE ERROR] {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }
}
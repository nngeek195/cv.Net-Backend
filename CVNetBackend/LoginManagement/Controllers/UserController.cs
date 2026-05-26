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

    // ✅ Inject the deletion services
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

    // ✅ NEW: The Master Deletion Endpoint
    [HttpDelete("delete-account")]
    [Authorize] // Requires valid JWT
    public async Task<IActionResult> DeleteAccount()
    {
        try
        {
            // Safely extract the ID of the person making the request
            var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(uid)) return Unauthorized();

            Console.WriteLine($"[CRITICAL] Initiating full system wipe for UID: {uid}");

            // 1. Wipe PostgreSQL (SQL Tables) using your existing UserService logic
            await _userService.DeleteFullUserProfile(uid);

            // 2. Wipe Firestore (NoSQL Document)
            await _firestoreService.DeleteUserDocument(uid);

            // 3. Wipe Firebase Auth Identity (Revokes all tokens instantly)
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
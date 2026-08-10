using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FirebaseAdmin.Auth;
using CVNetBackend.LoginManagement.Models;
using CVNetBackend.Services;
using System.Security.Claims;

namespace CVNetBackend.LoginManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly DatabaseService _db;
    private readonly FirestoreService _fs;

    public AuthController(DatabaseService db, FirestoreService fs)
    {
        _db = db;
        _fs = fs;
    }

    [HttpPost("signup")]
    [Authorize] 
    public async Task<IActionResult> SignUp([FromBody] SignupRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = string.Join("; ", ModelState.Values
                                    .SelectMany(x => x.Errors)
                                    .Select(x => x.ErrorMessage));
            return BadRequest(new { error = "Validation Failed", details = errors });
        }
        
        if (request.Agreement != "Agreed")
            return BadRequest(new { error = "Terms and Privacy Policy must be accepted." });

        try
        {
            var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(uid))
                return Unauthorized(new { error = "Identity token validation failed." });

            // 1. Send SPLIT names to Firestore (NoSQL)
            await _fs.CreateUserDocument(uid, request.FirstName, request.LastName, request.Email);
            
            // 2. Send COMBINED name to PostgreSQL (SQL)
            string combinedFullName = $"{request.FirstName} {request.LastName}".Trim();
            await _db.UpsertUserToPostgres(uid, request.Email, combinedFullName, request.Agreement);

            return Ok(new { message = "User successfully synchronized everywhere!", uid = uid });
        }
        catch (Exception ex)
        {
            Console.WriteLine("=========================================");
            Console.WriteLine("🚨 [CRITICAL DATABASE CRASH] 🚨");
            Console.WriteLine(ex.Message);
            Console.WriteLine("=========================================");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] TokenAuthRequest request)
    {
        Console.WriteLine("\n==================================================");
        Console.WriteLine("🛡️ [DEBUG-BACKEND] /api/Auth/login endpoint hit!");
        
        if (request == null || string.IsNullOrEmpty(request.IdToken)) 
        {
            Console.WriteLine("❌ [DEBUG-BACKEND] Request body or IdToken is missing!");
            return BadRequest(new { error = "Invalid request payload." });
        }
        
        Console.WriteLine($"[DEBUG-BACKEND] Token received snippet: {request.IdToken.Substring(0, Math.Min(15, request.IdToken.Length))}...");

        try
        {
            Console.WriteLine("[DEBUG-BACKEND] Attempting to verify token with Firebase Admin SDK...");
            FirebaseToken decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(request.IdToken);
            string uid = decodedToken.Uid;
            
            Console.WriteLine($"✅ [DEBUG-BACKEND] Token verified successfully! UID: {uid}");
            
            string email = decodedToken.Claims.ContainsKey("email") 
                ? decodedToken.Claims["email"]?.ToString() ?? "" 
                : "";
            
            string name = decodedToken.Claims.ContainsKey("name") 
                ? decodedToken.Claims["name"]?.ToString() ?? "CV User" 
                : "CV User";

            var parts = name.Trim().Split(' ');
            string firstName = parts[0];
            string lastName = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "";

            Console.WriteLine("[DEBUG-BACKEND] Syncing user to Firestore and Postgres...");
            await _fs.UpsertUserDocument(uid, firstName, lastName, email);
            await _db.UpsertUserToPostgres(uid, email, name, "Agreed");

            Console.WriteLine("✅ [DEBUG-BACKEND] Login and Sync Complete!");
            return Ok(new { 
                message = "Login and Sync Successful!", 
                uid = uid,
                email = email
            });
        }
        catch (FirebaseAuthException authEx)
        {
            Console.WriteLine($"\n❌ [DEBUG-BACKEND] FIREBASE AUTH REJECTION: {authEx.Message}");
            Console.WriteLine($"❌ [DEBUG-BACKEND] Auth Error Reason: {authEx.AuthErrorCode}\n");
            return Unauthorized(new { error = "Firebase token verification failed", details = authEx.Message });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ [DEBUG-BACKEND] GENERAL SYSTEM ERROR: {ex.Message}");
            Console.WriteLine($"❌ [DEBUG-BACKEND] Stack Trace: {ex.StackTrace}\n");
            return StatusCode(500, new { error = "Internal server error during login", details = ex.Message });
        }
    }
}
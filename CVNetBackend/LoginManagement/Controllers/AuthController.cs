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
        try
        {
            FirebaseToken decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(request.IdToken);
            string uid = decodedToken.Uid;
            
            string email = decodedToken.Claims.ContainsKey("email") 
                ? decodedToken.Claims["email"]?.ToString() ?? "" 
                : "";
            
            // Google gives us the Full Name in a single string
            string name = decodedToken.Claims.ContainsKey("name") 
                ? decodedToken.Claims["name"]?.ToString() ?? "CV User" 
                : "CV User";

            // 1. Split the Google Full Name for Firestore (NoSQL)
            var parts = name.Trim().Split(' ');
            string firstName = parts[0];
            string lastName = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "";

            await _fs.UpsertUserDocument(uid, firstName, lastName, email);

            // 2. Keep the Google Full Name intact for PostgreSQL (SQL)
            await _db.UpsertUserToPostgres(uid, email, name, "Agreed");

            return Ok(new { 
                message = "Login and Sync Successful!", 
                uid = uid,
                email = email
            });
        }
        catch (Exception ex)
        {
            return Unauthorized(new { error = "Authentication failed", details = ex.Message });
        }
    }
}
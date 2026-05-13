using Microsoft.AspNetCore.Mvc;
using FirebaseAdmin.Auth;
using CVNetBackend.LoginManagement.Models;
using CVNetBackend.Services;

namespace CVNetBackend.LoginManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly DatabaseService _db = new();
    private readonly FirestoreService _fs = new();

    [HttpPost("signup")]
    public async Task<IActionResult> SignUp([FromBody] SignupRequest request)
    {
        try
        {
            var userArgs = new UserRecordArgs
            {
                Email = request.Email,
                Password = request.Password,
                DisplayName = $"{request.FirstName} {request.LastName}"
            };
            var userRecord = await FirebaseAuth.DefaultInstance.CreateUserAsync(userArgs);

            await _fs.CreateUserDocument(userRecord.Uid, request.FirstName, request.LastName, request.Email);
            
            // Updated to use the unified method name
            await _db.UpsertUserToPostgres(userRecord.Uid, request.Email, $"{request.FirstName} {request.LastName}");

            return Ok(new { message = "User successfully created everywhere!", uid = userRecord.Uid });
        }
        catch (Exception ex)
        {
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
            
            // Fixed null warnings by providing fallback empty strings
            string email = decodedToken.Claims.ContainsKey("email") 
                ? decodedToken.Claims["email"]?.ToString() ?? "" 
                : "";
            
            string name = decodedToken.Claims.ContainsKey("name") 
                ? decodedToken.Claims["name"]?.ToString() ?? "CV User" 
                : "CV User";

            // Triple-Sync: PostgreSQL Upsert
            await _db.UpsertUserToPostgres(uid, email, name);

            return Ok(new { 
                message = "Authentication and Sync Successful!", 
                uid = uid,
                email = email
            });
        }
        catch (Exception ex)
        {
            return Unauthorized(new { error = "Invalid token or sync failed", details = ex.Message });
        }
    }
}
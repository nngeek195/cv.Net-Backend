using Microsoft.AspNetCore.Mvc;
using FirebaseAdmin.Auth;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    [HttpPost("initialize")]
    public async Task<IActionResult> InitializeUser()
    {
        // 1. Get the Token from the Header
        var authHeader = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return Unauthorized("Missing or invalid token");

        string idToken = authHeader.Substring(7); // Remove "Bearer " prefix

        try {
            // 2. Verify the Token with Firebase
            FirebaseToken decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(idToken);
            string uid = decodedToken.Uid; // THIS IS THE USER'S UNIQUE ID

            // 3. Now you know exactly who this is! 
            Console.WriteLine($"✅ Verified User: {uid}");
            

            return Ok(new { userId = uid, message = "Identity Verified!" });
        }
        catch {
            return Unauthorized("Token verification failed");
        }
    }
}
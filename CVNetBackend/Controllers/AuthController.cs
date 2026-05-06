using Microsoft.AspNetCore.Mvc;
using FirebaseAdmin.Auth;
using CVNetBackend.Models;
using CVNetBackend.Services;

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
            // STEP 1: Firebase Auth
            var userArgs = new UserRecordArgs
            {
                Email = request.Email,
                Password = request.Password,
                DisplayName = $"{request.FirstName} {request.LastName}"
            };
            var userRecord = await FirebaseAuth.DefaultInstance.CreateUserAsync(userArgs);

            // STEP 2: Firestore
            await _fs.CreateUserDocument(userRecord.Uid, request.FirstName, request.LastName, request.Email);

            // STEP 3: PostgreSQL
            await _db.SaveToPostgres(userRecord.Uid, request.FirstName, request.LastName, request.Email);

            return Ok(new { message = "User successfully created everywhere!", uid = userRecord.Uid });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
using Microsoft.AspNetCore.Mvc;
using CVNetBackend.ProfileHandler;

namespace CVNetBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly ProfileService _profileService;
    
    public ProfileController(ProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpPost("upload-image")]
    public async Task<IActionResult> UploadImage([FromForm] string userId, IFormFile file)
    {
        try 
        {
            var url = await _profileService.UploadProfileImageAsync(userId, file);
            return Ok(new { status = "success", imageUrl = url });
        }
        catch (Exception ex)
        {
            return BadRequest(new { status = "error", message = ex.Message });
        }
    }
}
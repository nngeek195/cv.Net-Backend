using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace CVNetBackend.User_End.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CVController : ControllerBase
{
    private readonly Cloudinary _cloudinary;

    public CVController()
    {
        string cloudName = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME") ?? "";
        string apiKey = Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY") ?? "";
        string apiSecret = Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET") ?? "";
        
        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            throw new Exception("One or more Cloudinary environment variables are missing.");
        }

        Account account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
        _cloudinary.Api.Secure = true;
    }

    [HttpPost("upload-cloudinary")]
    public async Task<IActionResult> UploadToCloudinary(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest(new { error = "No file was received." });

        try
        {
            using var stream = file.OpenReadStream();
            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "cv_documents" 
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null) return BadRequest(new { error = uploadResult.Error.Message });

            return Ok(new { url = uploadResult.SecureUrl.ToString() });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CLOUDINARY ERROR] {ex.Message}");
            return StatusCode(500, new { error = "Internal server error during upload." });
        }
    }
}
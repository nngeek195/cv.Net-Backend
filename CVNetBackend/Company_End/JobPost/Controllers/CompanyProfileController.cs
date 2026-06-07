using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CVNetBackend.Company_End.Services;
using CVNetBackend.Company_End.Models;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using System;
using Microsoft.AspNetCore.Http;

namespace CVNetBackend.Company_End.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CompanyProfileController : ControllerBase
{
    private readonly CompanyProfileService _profileService;

    public CompanyProfileController(CompanyProfileService profileService)
    {
        _profileService = profileService;
    }

    private string? GetUserEmail()
    {
        return User.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value;
    }

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        try
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return Unauthorized("Invalid token.");

            var profile = await _profileService.GetProfileAsync(email);
            if (profile == null) return NotFound("Company profile not found.");

            return Ok(profile);
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("update")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateCompanyDto dto)
    {
        try
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return Unauthorized("Invalid token.");

            var success = await _profileService.UpdateProfileAsync(email, dto);
            return success ? Ok(new { message = "Profile updated." }) : BadRequest("Failed to update profile.");
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("upload-logo")]
    public async Task<IActionResult> UploadLogo(IFormFile file)
    {
        try
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return Unauthorized("Invalid token.");

            var logoUrl = await _profileService.UploadLogoAsync(email, file);
            return Ok(new { status = "success", logoUrl });
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }
}
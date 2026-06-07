using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CVNetBackend.User_End.JobApply.Services;
using CVNetBackend.User_End.JobApply.Models;
using System.Security.Claims;
using System.Threading.Tasks;
using System;

namespace CVNetBackend.Candidate_End.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ApplicationController : ControllerBase
{
    private readonly ApplicationService _appService;

    public ApplicationController(ApplicationService appService)
    {
        _appService = appService;
    }

    [HttpGet("my-profiles")]
    public async Task<IActionResult> GetProfiles()
    {
        try {
            var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (uid == null) return Unauthorized();
            return Ok(await _appService.GetUserProfilesAsync(uid));
        } catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("profile-details/{profileId}")]
    public async Task<IActionResult> GetProfileDetails(string profileId)
    {
        try {
            var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (uid == null) return Unauthorized();
            return Ok(await _appService.GetProfileDetailsAsync(profileId, uid));
        } catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ✅ NEW ENDPOINT: Fetches a single application by ID for the details page
    [HttpGet("{id}")]
    public async Task<IActionResult> GetApplicationById(string id)
    {
        try {
            var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (uid == null) return Unauthorized();
            return Ok(await _appService.GetApplicationByIdAsync(id, uid));
        } catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] ApplyForJobDto dto)
    {
        try {
            var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (uid == null) return Unauthorized();
            
            await _appService.SubmitApplicationAsync(uid, dto);
            return Ok(new { message = "Application submitted successfully!" });
        } catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }
}
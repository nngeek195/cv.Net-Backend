using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CVNetBackend.Company_End.CandidateSection.Models;
using CVNetBackend.Company_End.CandidateSection.Services;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace CVNetBackend.Company_End.CandidateSection.Controllers;

[ApiController]
[Route("api/candidates")]
[Authorize] // 🔒 LOCK ENABLED: Only logged-in users can enter
public class CandidatesController : ControllerBase
{
    private readonly CandidateService _service;

    public CandidatesController(CandidateService service)
    {
        _service = service;
    }

    // Helper method to safely extract the email from the JWT
    private string? GetUserEmail()
    {
        return User.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value;
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> GetJobsForFilter()
    {
        try
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return Unauthorized(new { error = "Invalid token." });

            var jobs = await _service.GetActiveJobsAsync(email); // Pass email to service
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetCandidates(
        [FromQuery] string? jobId, 
        [FromQuery] string? sortOrder = "desc", 
        [FromQuery] string? search = null)
    {
        try
        {
            var email = GetUserEmail();
            if (string.IsNullOrEmpty(email)) return Unauthorized(new { error = "Invalid token." });

            var candidates = await _service.GetCandidatesAsync(email, jobId, sortOrder, search); // Pass email to service
            return Ok(candidates);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
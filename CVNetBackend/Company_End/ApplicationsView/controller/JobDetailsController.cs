using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using CVNetBackend.Company_End.ApplicationsView.Models;
using CVNetBackend.Company_End.ApplicationsView.Services;

namespace CVNetBackend.Company_End.ApplicationsView.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class JobDetailsController : ControllerBase
{
private readonly JobDetailsService _service;
private readonly ILogger<JobDetailsController> _logger;


public JobDetailsController(
    JobDetailsService service,
    ILogger<JobDetailsController> logger)
{
    _service = service;
    _logger = logger;
}

[HttpGet("{jobId}")]
public async Task<IActionResult> GetJobDashboard(string jobId)
{
    try
    {
        var details = await _service.GetJobDetailsAsync(jobId);

        if (details == null)
            return NotFound(new { error = "Job not found." });

        var applicants = await _service.GetApplicantsAsync(jobId);

        return Ok(new
        {
            details,
            applicants
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to load dashboard for Job {JobId}", jobId);

        return StatusCode(500, new
        {
            error = "Failed to load job dashboard."
        });
    }
}

[HttpPost("{jobId}/close")]
public async Task<IActionResult> CloseJob(string jobId)
{
    try
    {
        var hrEmail = User.FindFirst(ClaimTypes.Email)?.Value;

        await _service.CloseJobAndRejectPendingAsync(jobId, hrEmail);

        return Ok(new
        {
            message = "Job closed and pending applicants rejected."
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to close Job {JobId}", jobId);

        return StatusCode(500, new
        {
            error = ex.Message
        });
    }
}

[HttpPost("{jobId}/repost")]
public async Task<IActionResult> RepostJob(string jobId)
{
    try
    {
        var newId = await _service.RepostJobAsync(jobId);

        return Ok(new
        {
            newJobId = newId
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to repost Job {JobId}", jobId);

        return StatusCode(500, new
        {
            error = ex.Message
        });
    }
}

[HttpPost("applicant/{appId}/interview")]
public async Task<IActionResult> CallForInterview(
    string appId,
    [FromBody] InterviewRequestDto dto)
{
    try
    {
        await _service.CallForInterviewAsync(appId, dto);

        return Ok(new
        {
            success = true
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to call applicant {AppId} for interview", appId);

        return StatusCode(500, new
        {
            error = ex.Message
        });
    }
}

[HttpPost("applicant/{appId}/reject")]
public async Task<IActionResult> RejectApplicant(
    string appId,
    [FromBody] RejectRequestDto dto)
{
    try
    {
        await _service.RejectApplicantAsync(appId, dto);

        return Ok(new
        {
            success = true
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to reject applicant {AppId}", appId);

        return StatusCode(500, new
        {
            error = ex.Message
        });
    }
}

[HttpGet("applicant-profile/{appId}")]
public async Task<IActionResult> GetApplicantFullProfile(string appId)
{
    try
    {
        var profile = await _service.GetFullApplicantProfileAsync(appId);

        if (profile == null)
        {
            return NotFound(new
            {
                error = "Applicant profile not found."
            });
        }

        return Ok(profile);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to load applicant profile {AppId}", appId);

        return StatusCode(500, new
        {
            error = ex.Message
        });
    }
}


}

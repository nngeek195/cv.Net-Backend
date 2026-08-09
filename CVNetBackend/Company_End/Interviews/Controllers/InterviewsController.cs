using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CVNetBackend.Company_End.Interviews.Models;
using CVNetBackend.Company_End.Interviews.Services;
using CVNetBackend.Company_End.ApplicationsView.Services; // Required to fetch isolated profiles
using System.Threading.Tasks;
using System;

namespace CVNetBackend.Company_End.Interviews.Controllers;

[ApiController]
[Route("api/interviews")]
// [Authorize] // Keep disabled until testing is done
public class InterviewsController : ControllerBase
{
    private readonly InterviewService _service;
    private readonly JobDetailsService _jobService; 

    public InterviewsController(InterviewService service, JobDetailsService jobService)
    {
        _service = service;
        _jobService = jobService;
    }

    [HttpGet]
    public async Task<IActionResult> GetInterviews()
    {
        try { return Ok(await _service.GetAllInterviewsAsync()); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpPut("{callId}/schedule")]
    public async Task<IActionResult> ScheduleInterview(string callId, [FromBody] ScheduleInterviewDto dto)
    {
        try 
        { 
            await _service.ScheduleInterviewAsync(callId, dto.InterviewDate);
            return Ok(new { success = true });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpPost("{callId}/reject")]
    public async Task<IActionResult> RejectCandidate(string callId, [FromBody] RejectInterviewDto dto)
    {
        try 
        { 
            await _service.RejectCandidateAsync(callId, dto.Reason);
            return Ok(new { success = true });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpPost("share-portal")]
    public async Task<IActionResult> CreatePortal([FromBody] CreatePortalRequestDto dto)
    {
        try 
        {
            var (portalId, password) = await _service.CreateSharedPortalAsync(dto);
            return Ok(new { 
                link = $"/board/{portalId}", // Secure frontend route
                password = password 
            });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpGet("portals")]
    public async Task<IActionResult> GetActivePortals()
    {
        try { return Ok(await _service.GetActivePortalsAsync()); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    [HttpDelete("portals/{portalId}")]
    public async Task<IActionResult> DeletePortal(string portalId)
    {
        try 
        { 
            await _service.DeletePortalAsync(portalId);
            return Ok(new { success = true });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SECURE PORTAL GATEWAYS (Strict PIN required)
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("shared/{portalId}/data")]
    [AllowAnonymous] 
public async Task<IActionResult> GetPortalData(string portalId, [FromHeader(Name = "X-Portal-PIN")] string pin)
{
    Console.WriteLine($"\n[DEBUG - CONTROLLER] Request received for Portal: {portalId}");
    Console.WriteLine($"[DEBUG - CONTROLLER] X-Portal-PIN Header provided: {!string.IsNullOrEmpty(pin)}");

    try 
    { 
        if (string.IsNullOrEmpty(pin)) 
        {
            Console.WriteLine("[DEBUG - CONTROLLER] 🚨 Missing PIN Header!");
            return Unauthorized(new { error = "Access PIN required." });
        }
        
        var data = await _service.GetPortalDataAsync(portalId, pin);
        Console.WriteLine($"[DEBUG - CONTROLLER] Returning OK with {data.Count()} job groups.");
        return Ok(data); 
    }
    catch (UnauthorizedAccessException ex) 
    { 
        Console.WriteLine($"[DEBUG - CONTROLLER] 🚨 Unauthorized: {ex.Message}");
        return Unauthorized(new { error = ex.Message }); 
    }
    catch (Exception ex) 
    { 
        Console.WriteLine($"[DEBUG - CONTROLLER] 🚨 Fatal Error: {ex.Message}");
        return StatusCode(500, new { error = ex.Message }); 
    }
}

    [HttpGet("shared/{portalId}/applicant/{appId}")]
    [AllowAnonymous] 
    public async Task<IActionResult> GetSharedApplicantProfile(string portalId, string appId, [FromHeader(Name = "X-Portal-PIN")] string pin)
    {
        try 
        {
            if (string.IsNullOrEmpty(pin)) return Unauthorized(new { error = "Access PIN required." });

            // 1. Verify PIN and mathematically prove Candidate belongs to this board
            bool isAuthorized = await _service.VerifyCandidateInPortalAsync(portalId, pin, appId);
            if (!isAuthorized) return Unauthorized(new { error = "Security Violation: Unauthorized data access." });

            // 2. Fetch isolated profile only if check passed
            var profile = await _jobService.GetFullApplicantProfileAsync(appId);
            if (profile == null) return NotFound();
            
            return Ok(profile);
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }
}
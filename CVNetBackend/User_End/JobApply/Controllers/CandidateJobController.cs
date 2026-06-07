using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using System;
using System.Security.Claims; // ✅ Added for Claims

using CVNetBackend.User_End.JobApply.Services;

namespace CVNetBackend.User_End.JobApply.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CandidateJobController : ControllerBase
{
    private readonly CandidateJobService _jobService;

    public CandidateJobController(CandidateJobService jobService)
    {
        _jobService = jobService;
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActiveJobs()
    {
        try { 
            // ✅ Extract User ID from the token
            var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (uid == null) return Unauthorized();

            // ✅ Pass User ID to the service
            return Ok(await _jobService.GetActiveJobsAsync(uid)); 
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        try { return Ok(await _jobService.GetCategoriesAsync()); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }
}
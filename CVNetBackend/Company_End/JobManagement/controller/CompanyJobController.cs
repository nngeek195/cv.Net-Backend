using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Threading.Tasks;
using System;
using CVNetBackend.Company_End.JobManagement.Services;

namespace CVNetBackend.Company_End.JobManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CompanyJobController : ControllerBase
{
    private readonly CompanyJobService _jobService;

    public CompanyJobController(CompanyJobService jobService)
    {
        _jobService = jobService;
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetJobsList()
    {
        try 
        {
            // Assuming the logged-in user is the HR representative linked via Email
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

            var jobs = await _jobService.GetCompanyJobsAsync(userEmail);
            return Ok(jobs);
        }
        catch (Exception ex) 
        { 
            return BadRequest(new { error = ex.Message }); 
        }
    }
}
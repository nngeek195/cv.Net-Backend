using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CVNetBackend.Company_End.Services;
using CVNetBackend.Company_End.Models;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace CVNetBackend.Company_End.Controllers;

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

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        try { return Ok(await _jobService.GetCategoriesAndRolesAsync()); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateJob([FromBody] CreateJobDto dto)
    {
        try
        {
            // ✅ BULLETPROOF FIX: Checks for BOTH standard JWT format and .NET's complex mapped schema format
            var email = User.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email)?.Value;
            
            if (string.IsNullOrEmpty(email)) 
                return Unauthorized(new { error = "Invalid token: Security context is missing the email claim." });

            var jobId = await _jobService.CreateJobAsync(email, dto);
            return Ok(new { message = "Job successfully posted!", jobId });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
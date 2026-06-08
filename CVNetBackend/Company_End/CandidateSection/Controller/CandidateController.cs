using Microsoft.AspNetCore.Mvc;
using CVNetBackend.Company_End.CandidateSection.Models;
using CVNetBackend.Company_End.CandidateSection.Services;
using System;
using System.Threading.Tasks;

namespace CVNetBackend.Company_End.CandidateSection.Controllers;

[ApiController]
[Route("api/candidates")] // <-- Hardcoded route to match frontend exactly
// [Authorize] <-- Keep this commented out for now while testing
public class CandidatesController : ControllerBase
{
    private readonly CandidateService _service;

    public CandidatesController(CandidateService service)
    {
        _service = service;
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> GetJobsForFilter()
    {
        try
        {
            var jobs = await _service.GetActiveJobsAsync();
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
            var candidates = await _service.GetCandidatesAsync(jobId, sortOrder, search);
            return Ok(candidates);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CVNetBackend.JobRoleManager.Services;
using System.Security.Claims;

namespace CVNetBackend.JobRoleManager.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Protected by Firebase Token Validation middleware
public class JobRoleController : ControllerBase
{
    private readonly SkillMatrixEngine _matrixEngine;

    public JobRoleController(SkillMatrixEngine matrixEngine)
    {
        _matrixEngine = matrixEngine;
    }

    [HttpGet("readiness-matrix")]
    public async Task<IActionResult> GetUserReadinessMatrix()
    {
        try
        {
            // Extract the verified security identifier from the validated Bearer token context
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "User context identification failed." });

            var report = await _matrixEngine.CalculateUserReadinessAsync(userId);
            return Ok(report);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
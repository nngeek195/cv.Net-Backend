using Microsoft.AspNetCore.Mvc;
using CVNetBackend.User_End.Enhancer;

namespace CVNetBackend.User_End.Controllers;

[ApiController]
[Route("api/[controller]")] // This makes the URL: api/Enhance
public class EnhanceController : ControllerBase
{
    private readonly EnhancerService _enhancer;

    public EnhanceController(EnhancerService enhancer)
    {
        _enhancer = enhancer;
    }

    [HttpPost]
    public async Task<IActionResult> Enhance([FromForm] string text, [FromForm] string mode, [FromForm] string? instruction)
    {
        var result = await _enhancer.EnhanceTextAsync(text, mode, instruction);
        return Ok(new { status = "success", enhanced_text = result });
    }
}
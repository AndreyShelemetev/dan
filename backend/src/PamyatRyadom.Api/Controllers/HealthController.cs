using Microsoft.AspNetCore.Mvc;

namespace PamyatRyadom.Api.Controllers;

[ApiController]
[Route("api/v1/health")]
public sealed class HealthController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;

    public HealthController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            service = "PamyatRyadom.Api",
            environment = _environment.EnvironmentName,
            timestamp = DateTimeOffset.UtcNow
        });
    }
}

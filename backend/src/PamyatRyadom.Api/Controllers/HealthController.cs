using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Data;

namespace PamyatRyadom.Api.Controllers;

[ApiController]
[Route("api/v1/health")]
public sealed class HealthController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly AppDbContext _dbContext;

    public HealthController(IWebHostEnvironment environment, AppDbContext dbContext)
    {
        _environment = environment;
        _dbContext = dbContext;
    }

    /// <summary>Liveness: process is up. Deliberately checks nothing else — a container
    /// orchestrator restarting the api because the database is briefly down would be
    /// exactly the wrong reaction. Use <see cref="Ready"/> for dependency health.</summary>
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

    /// <summary>Readiness: the api can actually serve traffic — Postgres answers and the
    /// schema it answers with has every migration this build expects. "ok" from
    /// <see cref="Get"/> only ever meant the process started.</summary>
    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        bool canConnect;
        try
        {
            canConnect = await _dbContext.Database.CanConnectAsync(ct);
        }
        catch
        {
            canConnect = false;
        }

        if (!canConnect)
        {
            return Unavailable("database_unreachable");
        }

        try
        {
            var pending = await _dbContext.Database.GetPendingMigrationsAsync(ct);
            if (pending.Any())
            {
                return Unavailable("pending_migrations");
            }
        }
        catch
        {
            // Can't confirm the schema matches this build — fail closed rather than
            // report ready on a check we could not actually complete.
            return Unavailable("migration_check_failed");
        }

        return Ok(new
        {
            status = "ready",
            service = "PamyatRyadom.Api",
            timestamp = DateTimeOffset.UtcNow
        });
    }

    private ObjectResult Unavailable(string reason) =>
        StatusCode(StatusCodes.Status503ServiceUnavailable, new
        {
            status = "unavailable",
            reason,
            service = "PamyatRyadom.Api",
            timestamp = DateTimeOffset.UtcNow
        });
}

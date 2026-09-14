using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using PamyatRyadom.Api.Controllers;
using PamyatRyadom.Api.Data;

namespace PamyatRyadom.Api.Tests;

/// <summary>
/// The readiness endpoint's whole point is to tell a container orchestrator "don't route
/// traffic here" when Postgres can't actually be reached — unlike liveness (<see
/// cref="HealthEndpointTests"/>), which is checked against a real Testcontainers database and
/// says nothing about outages. Pointing a fresh <see cref="AppDbContext"/> at a port nothing is
/// listening on reproduces "database unreachable" without needing Docker at all, so this runs
/// as a plain unit test.
/// </summary>
public sealed class HealthReadinessTests
{
    [Fact]
    public async Task Ready_returns_503_when_the_database_is_unreachable()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=127.0.0.1;Port=1;Database=nonexistent;Username=nonexistent;Password=nonexistent;" +
                "Timeout=2")
            .Options;

        await using var dbContext = new AppDbContext(options);
        var controller = new HealthController(new StubWebHostEnvironment(), dbContext);

        var result = await controller.Ready(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "PamyatRyadom.Api.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

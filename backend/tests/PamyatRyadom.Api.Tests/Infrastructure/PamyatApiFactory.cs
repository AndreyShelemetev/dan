using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace PamyatRyadom.Api.Tests.Infrastructure;

/// <summary>
/// WebApplicationFactory wired to a real (Testcontainers) Postgres connection string, mirroring
/// NaidiAI's NaidiApiFactory so the pattern ports across as later tasks add entities/migrations.
/// </summary>
public sealed class PamyatApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public PamyatApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning"
            });
        });
    }
}

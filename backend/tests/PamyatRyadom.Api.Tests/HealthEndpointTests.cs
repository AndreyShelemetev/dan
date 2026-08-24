using System.Net;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests;

/// <summary>
/// End-to-end smoke test: boots the API via WebApplicationFactory against a real Testcontainers
/// Postgres instance and confirms GET /api/v1/health returns 200. This proves the full harness
/// (DbContext registration + Npgsql connection + controller routing) works together before any
/// module-specific tests are added.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class HealthEndpointTests
{
    private readonly PostgresFixture _postgres;

    public HealthEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        await using var factory = new PamyatApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"ok\"", body);
    }
}

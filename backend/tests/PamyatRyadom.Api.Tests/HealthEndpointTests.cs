using System.Net;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests;

/// <summary>
/// End-to-end smoke test: boots the API via WebApplicationFactory against a real Testcontainers
/// Postgres instance and confirms GET /api/v1/health returns 200. This proves the full harness
/// (DbContext registration + Npgsql connection + migrations + controller routing) works together
/// before any module-specific test is even considered.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class HealthEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;

    private PamyatApiFactory? _factory;

    public HealthEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = new PamyatApiFactory(await _postgres.CreateDatabaseAsync());
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        NpgsqlConnection.ClearAllPools();
    }

    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        var client = _factory!.CreateApiClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("ok", response.Json?["status"]?.GetValue<string>());
        Assert.Equal("PamyatRyadom.Api", response.Json?["service"]?.GetValue<string>());
    }

    [Fact]
    public async Task The_auth_schema_is_migrated_into_the_test_database()
    {
        // Guards the harness itself: the api container does not migrate on startup, so if the
        // factory ever stops doing it every auth test would fail with "relation does not exist".
        var tables = await _factory!.QueryDbAsync(db => db.Database
            .SqlQueryRaw<string>("""SELECT table_name AS "Value" FROM information_schema.tables WHERE table_schema = 'public'""")
            .ToListAsync());

        string[] expected =
        [
            "users", "auth_identities", "auth_sessions", "otp_codes", "mfa_secrets",
            "consent_logs", "legal_documents", "legal_acceptances", "security_audit_logs"
        ];

        Assert.All(expected, table => Assert.Contains(table, tables));
    }
}

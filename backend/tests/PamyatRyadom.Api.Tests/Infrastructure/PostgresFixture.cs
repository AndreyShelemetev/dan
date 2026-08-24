using Testcontainers.PostgreSql;

namespace PamyatRyadom.Api.Tests.Infrastructure;

/// <summary>
/// Starts one Postgres container (postgres:17-alpine, matching docker-compose.yml) for the test
/// run. Requires Docker to be available locally — see README.md for local setup.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("pamyat_ryadom_tests")
        .WithUsername("pamyat_ryadom_tests")
        .WithPassword("pamyat_ryadom_tests")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

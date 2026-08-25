using Microsoft.EntityFrameworkCore;
using Npgsql;
using PamyatRyadom.Api.Data;
using Testcontainers.PostgreSql;

namespace PamyatRyadom.Api.Tests.Infrastructure;

/// <summary>
/// Starts one Postgres container (postgres:17-alpine, matching docker-compose.yml) for the test
/// run. Requires Docker to be available locally — see README.md for local setup.
///
/// Isolation is per test, not per run: <see cref="CreateDatabaseAsync"/> hands every test its own
/// freshly created database, so nothing a test writes (users, sessions, OTP rows, audit trail) can
/// leak into another one. The migrations are applied exactly once, into a template database, and
/// every per-test database is then created with <c>CREATE DATABASE ... TEMPLATE</c> — a file copy
/// inside Postgres, far cheaper than re-running the migrations tens of times.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    /// <summary>Migrated once in <see cref="InitializeAsync"/>; never connected to afterwards, since
    /// Postgres refuses to copy a template that has live connections.</summary>
    private const string TemplateDatabase = "pamyat_ryadom_template";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("pamyat_ryadom_tests")
        .WithUsername("pamyat_ryadom_tests")
        .WithPassword("pamyat_ryadom_tests")
        .Build();

    private int _databaseCounter;

    /// <summary>The container's own database. Used only to issue CREATE DATABASE — never by a test.</summary>
    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await BuildTemplateDatabaseAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Creates an empty-but-migrated database and returns its connection string.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"pamyat_test_{Interlocked.Increment(ref _databaseCounter):D4}";
        await ExecuteAsync(ConnectionString, $"CREATE DATABASE \"{name}\" TEMPLATE \"{TemplateDatabase}\"");
        return ConnectionStringFor(name);
    }

    private async Task BuildTemplateDatabaseAsync()
    {
        await ExecuteAsync(ConnectionString, $"CREATE DATABASE \"{TemplateDatabase}\"");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionStringFor(TemplateDatabase))
            .UseSnakeCaseNamingConvention()
            .Options;

        await using (var db = new AppDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        // Postgres will not use a template that still has a connection open against it.
        NpgsqlConnection.ClearAllPools();
    }

    private string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = database,
            // One pool per test database, and a lot of test databases: keep each one small so the
            // server's connection limit is never the reason a run fails.
            MaxPoolSize = 10
        }.ConnectionString;

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

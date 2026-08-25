using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PamyatRyadom.Api.Models.Auth;

namespace PamyatRyadom.Api.Tests.Infrastructure;

/// <summary>
/// Base for the auth integration tests. Every test gets its own database (so no test can observe
/// another one's users, sessions or audit rows) and builds its own host — which also means a fresh
/// rate limiter, since the limiter's state lives in the host, not in Postgres.
/// </summary>
public abstract class AuthIntegrationTest : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly List<PamyatApiFactory> _factories = new();
    private readonly List<string> _keyRingPaths = new();

    protected AuthIntegrationTest(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    /// <summary>This test's private database.</summary>
    protected string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        ConnectionString = await _postgres.CreateDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        foreach (var factory in _factories)
        {
            await factory.DisposeAsync();
        }

        // Each test uses a database of its own, so its pool would otherwise sit on idle connections
        // for the rest of the run and eventually hit the server's connection limit.
        NpgsqlConnection.ClearAllPools();

        foreach (var path in _keyRingPaths.Where(Directory.Exists))
        {
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A leftover temp directory is not worth failing a green test over.
            }
        }
    }

    /// <summary>Builds a host against this test's database. Call it more than once to model a
    /// restart/redeploy — pass the same <paramref name="dataProtectionKeyRingPath"/> to keep the
    /// Data Protection key ring, or a different one to model losing it.</summary>
    protected PamyatApiFactory CreateFactory(
        IReadOnlyDictionary<string, string?>? settings = null,
        string? dataProtectionKeyRingPath = null,
        CapturingEmailSender? emails = null)
    {
        var factory = new PamyatApiFactory(
            ConnectionString,
            settings,
            dataProtectionKeyRingPath ?? NewKeyRingPath(),
            emails);

        _factories.Add(factory);
        return factory;
    }

    protected string NewKeyRingPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "pamyat-ryadom-tests", Guid.NewGuid().ToString("N"));
        _keyRingPaths.Add(path);
        return path;
    }

    protected static Dictionary<string, string?> Settings(params (string Key, string Value)[] entries) =>
        entries.ToDictionary(entry => entry.Key, entry => (string?)entry.Value);

    // ---------------------------------------------------------------------------------------------
    // Login flow helpers
    // ---------------------------------------------------------------------------------------------

    protected const string OtpRequestPath = "/api/v1/auth/otp/request";
    protected const string OtpVerifyPath = "/api/v1/auth/otp/verify";
    protected const string MePath = "/api/v1/auth/me";
    protected const string LogoutPath = "/api/v1/auth/logout";
    protected const string LogoutAllPath = "/api/v1/auth/logout-all";
    protected const string MfaEnrollPath = "/api/v1/auth/mfa/enroll";
    protected const string MfaVerifyPath = "/api/v1/auth/mfa/verify";

    protected static object OtpRequest(string destination, string channel = OtpChannels.Email, string? purpose = null) =>
        new { destination, channel, purpose };

    protected static object OtpVerify(string destination, string code, string channel = OtpChannels.Email, string? purpose = null) =>
        new { destination, channel, purpose, code };

    /// <summary>Requests a login code and returns it, read out of the captured email.</summary>
    protected static async Task<string> RequestCodeAsync(PamyatApiFactory factory, ApiClient client, string email)
    {
        var response = await client.PostAsync(OtpRequestPath, OtpRequest(email));
        Assert.Equal(HttpStatusCode.OK, response.Status);

        return factory.Emails.RequireLastCodeTo(email.Trim().ToLowerInvariant());
    }

    /// <summary>Full login: request a code, verify it, leave <paramref name="client"/> holding the
    /// session cookie.</summary>
    protected static async Task<ApiResult> LoginAsync(PamyatApiFactory factory, ApiClient client, string email)
    {
        var code = await RequestCodeAsync(factory, client, email);
        var response = await client.PostAsync(OtpVerifyPath, OtpVerify(email, code));

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.NotNull(response.SetSessionToken);

        return response;
    }

    /// <summary>A six-digit code guaranteed to differ from <paramref name="code"/>.</summary>
    protected static string WrongCode(string code) =>
        new(code.Select(digit => (char)('0' + ((digit - '0' + 5) % 10))).ToArray());

    // ---------------------------------------------------------------------------------------------
    // Database helpers
    // ---------------------------------------------------------------------------------------------

    /// <summary>Rewrites a session's timestamps so a test can reach a rotation threshold or an
    /// absolute-lifetime wall without waiting days. Preferred over a clock abstraction: the code
    /// under test reads <c>DateTimeOffset.UtcNow</c> directly, and adding one just for the tests
    /// would change production code to suit them.</summary>
    protected static async Task BackdateSessionAsync(
        PamyatApiFactory factory,
        long sessionId,
        TimeSpan createdAgo,
        TimeSpan? rotatedAgo = null,
        TimeSpan? expiresIn = null,
        bool? isPrivileged = null)
    {
        await factory.WithDbAsync(async db =>
        {
            var session = await db.AuthSessions.SingleAsync(x => x.Id == sessionId);
            var now = DateTimeOffset.UtcNow;

            session.CreatedAt = now - createdAgo;

            if (rotatedAgo is not null)
            {
                session.RotatedAt = now - rotatedAgo.Value;
            }

            if (expiresIn is not null)
            {
                session.ExpiresAt = now + expiresIn.Value;
            }

            if (isPrivileged is not null)
            {
                session.IsPrivileged = isPrivileged.Value;
            }

            await db.SaveChangesAsync();
        });
    }

    protected static Task<AuthSession> SingleSessionAsync(PamyatApiFactory factory) =>
        factory.QueryDbAsync(db => db.AuthSessions.SingleAsync());

    protected static Task<User> SingleUserAsync(PamyatApiFactory factory) =>
        factory.QueryDbAsync(db => db.Users.SingleAsync());

    /// <summary>Puts the (single) user of this test's database into another role — the only way to
    /// get a non-client account, since OTP registration always creates a client.</summary>
    protected static async Task SetRoleAsync(PamyatApiFactory factory, string role)
    {
        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync();
            user.Role = role;
            await db.SaveChangesAsync();
        });
    }

    protected static async Task SetUserStatusAsync(PamyatApiFactory factory, string status)
    {
        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync();
            user.Status = status;
            await db.SaveChangesAsync();
        });
    }

    /// <summary>The metadata of the newest audit row of <paramref name="eventType"/>. Parsed rather
    /// than returned as text because the column is <c>jsonb</c>: Postgres re-renders it on the way
    /// out (key order, whitespace), so matching on substrings of the original would be brittle.</summary>
    protected static Task<JsonNode?> LatestAuditMetadataAsync(PamyatApiFactory factory, string eventType) =>
        factory.QueryDbAsync(async db =>
        {
            var row = await db.SecurityAuditLogs
                .Where(x => x.EventType == eventType)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();

            var raw = row?.Metadata?.RootElement.GetRawText();
            return raw is null ? null : JsonNode.Parse(raw);
        });

    protected static Task<int> AuditCountAsync(PamyatApiFactory factory, string eventType) =>
        factory.QueryDbAsync(db => db.SecurityAuditLogs.CountAsync(x => x.EventType == eventType));
}

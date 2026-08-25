using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PamyatRyadom.Api.Data;
using PamyatRyadom.Api.Services.Auth;

namespace PamyatRyadom.Api.Tests.Infrastructure;

/// <summary>
/// WebApplicationFactory wired to a real (Testcontainers) Postgres database.
///
/// Four things it does beyond pointing at a connection string:
/// <list type="bullet">
/// <item>gets the settings in early enough to be seen (see <see cref="EnsureStarted"/>);</item>
/// <item>applies the EF Core migrations before the first request, so the auth tables actually exist
/// in the per-test database;</item>
/// <item>swaps <see cref="IEmailSender"/> for <see cref="CapturingEmailSender"/> — the host runs
/// outside Development, where Program.cs resolves the real SMTP sender;</item>
/// <item>adds this test assembly as an MVC application part, so <see cref="RoleProbeController"/>
/// can exercise <see cref="RequireRoleFilter"/> role sets that no production endpoint uses yet.</item>
/// </list>
///
/// Auth behaviour is driven entirely by configuration (<c>Auth:*</c>), so a test that needs a
/// different attempt limit or session ceiling passes it in rather than reaching into the service.
/// </summary>
public sealed class PamyatApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Serializes host startup across factories, because <see cref="EnsureStarted"/> has to
    /// put settings into the process environment while the entry point runs. The test collections are
    /// already sequential (xunit.runner.json), so this only guards against a test holding two
    /// factories at once.</summary>
    private static readonly Lock StartLock = new();

    private readonly Dictionary<string, string?> _settings;

    private bool _started;

    public PamyatApiFactory(
        string connectionString,
        IReadOnlyDictionary<string, string?>? settings = null,
        string? dataProtectionKeyRingPath = null,
        CapturingEmailSender? emails = null)
    {
        Emails = emails ?? new CapturingEmailSender();
        DataProtectionKeyRingPath = dataProtectionKeyRingPath
                                    ?? Path.Combine(Path.GetTempPath(), "pamyat-ryadom-tests", Guid.NewGuid().ToString("N"));

        _settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["Logging:LogLevel:Default"] = "Warning",
            ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",
            ["DataProtection:KeyRingPath"] = DataProtectionKeyRingPath
        };

        if (settings is not null)
        {
            foreach (var setting in settings)
            {
                _settings[setting.Key] = setting.Value;
            }
        }
    }

    /// <summary>The login codes this host "sent" — the test's only way to learn a code, since the
    /// database stores nothing but its PBKDF2 hash.</summary>
    public CapturingEmailSender Emails { get; }

    /// <summary>Where this host persists its Data Protection key ring. Pass the same path to a second
    /// factory to model a redeploy that must keep stored MFA secrets readable.</summary>
    public string DataProtectionKeyRingPath { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(_settings));

        builder.ConfigureTestServices(services =>
        {
            // Program.cs picks the sender by environment, and "Testing" is not Development — without
            // this every OTP test would try to open a real SMTP connection.
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);

            services.AddControllers().AddApplicationPart(typeof(PamyatApiFactory).Assembly);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // The api container deliberately does not migrate on startup (see CLAUDE.md), so the test
        // harness has to — otherwise every auth test would fail with "relation does not exist".
        using (var scope = host.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
        }

        return host;
    }

    /// <summary>An HTTP client that carries the session cookie by hand, so a test can inspect
    /// rotation, present a stale token, or send deliberate garbage.</summary>
    public ApiClient CreateApiClient()
    {
        EnsureStarted();

        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            // Cookies are handled by ApiClient, and a redirect would hide the status code under test.
            HandleCookies = false,
            AllowAutoRedirect = false
        });

        return new ApiClient(client, Services.GetRequiredService<IOptions<AuthOptions>>().Value.CookieName);
    }

    /// <summary>Runs <paramref name="action"/> against the same database the API writes to, in its own
    /// scope — for arranging state (backdating a session) and for asserting what actually landed.</summary>
    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        EnsureStarted();

        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Value-returning twin of <see cref="WithDbAsync"/>, named apart from it so a lambda
    /// can never pick the wrong overload.</summary>
    public async Task<T> QueryDbAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        EnsureStarted();

        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>
    /// Boots the host with this factory's settings visible to Program.cs's *eager* configuration
    /// reads.
    ///
    /// Under minimal hosting the entry point runs top to bottom before WebApplicationFactory gets to
    /// contribute anything, so <c>ConfigureAppConfiguration</c> lands too late for the two values
    /// Program.cs reads while building: the connection string (read at line 20 — miss it and
    /// <c>AppDbContext</c> is never registered at all) and the Data Protection key ring path.
    /// Everything bound through the options system (<c>Auth:*</c>) resolves lazily and picks the
    /// in-memory source up regardless; only these startup-time reads need the environment.
    ///
    /// The variables are removed again as soon as the host is up, so they never bleed into another
    /// factory or another test.
    /// </summary>
    private void EnsureStarted()
    {
        if (_started)
        {
            return;
        }

        lock (StartLock)
        {
            if (_started)
            {
                return;
            }

            var restore = ApplySettingsToEnvironment();
            try
            {
                // Touching Services is what runs the entry point and builds the host.
                _ = Services;
            }
            finally
            {
                restore();
            }

            _started = true;
        }
    }

    private Action ApplySettingsToEnvironment()
    {
        var previous = new Dictionary<string, string?>(_settings.Count);

        foreach (var (key, value) in _settings)
        {
            var name = key.Replace(":", "__", StringComparison.Ordinal);
            previous[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        return () =>
        {
            foreach (var (name, value) in previous)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        };
    }
}

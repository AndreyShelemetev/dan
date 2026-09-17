using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PamyatRyadom.Api.Services.Media;
using PamyatRyadom.Api.Services.Payments;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests.Payments;

/// <summary>
/// Program.cs picks the payment provider by <see cref="IHostEnvironment"/>, not by configuration,
/// and Production is supposed to fail at boot rather than the first time a client tries to pay.
/// No Testcontainers here: <c>ConnectionStrings:DefaultConnection</c> is left unset, so
/// <c>AppDbContext</c> is never registered and nothing in these tests needs a real Postgres.
///
/// Settings go in as environment variables, not <c>ConfigureAppConfiguration</c>: under minimal
/// hosting the entry point runs top to bottom before <c>WebApplicationFactory</c> gets a chance to
/// contribute anything, so that hook lands too late for the eager config read in Program.cs (same
/// reason <see cref="PamyatApiFactory"/> does it this way for the connection string).
/// </summary>
public sealed class PaymentProviderRegistrationTests
{
    private static readonly Dictionary<string, string?> YooKassaSettings = new()
    {
        ["YooKassa__ShopId"] = "shop-1",
        ["YooKassa__SecretKey"] = "secret-1",
        ["Payments__ReturnUrlBase"] = "https://pamyat-ryadom.ru",
    };

    [Fact]
    public void Production_without_yookassa_config_refuses_to_start()
    {
        using var restore = ApplyEnvironment(new Dictionary<string, string?>());
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

        var ex = Assert.ThrowsAny<Exception>(() => factory.Services);
        Assert.Contains("YOOKASSA_SHOP_ID", RootCause(ex).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_with_full_yookassa_config_starts_and_registers_the_real_provider()
    {
        using var restore = ApplyEnvironment(YooKassaSettings);
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var scope = factory.Services.CreateScope();

        Assert.IsType<YooKassaPaymentProvider>(scope.ServiceProvider.GetRequiredService<IPaymentProvider>());
    }

    [Fact]
    public void Development_still_uses_the_stub_regardless_of_yookassa_config()
    {
        using var restore = ApplyEnvironment(YooKassaSettings);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                // Development also runs a one-time IObjectStorage.EnsureBucketAsync() against a
                // real S3 endpoint (see Program.cs) — this test is about provider selection, not
                // media, so swap in the in-memory fake the rest of the suite already uses.
                services.RemoveAll<IObjectStorage>();
                services.AddSingleton<IObjectStorage>(new InMemoryObjectStorage());
            });
        });
        using var scope = factory.Services.CreateScope();

        Assert.IsType<StubPaymentProvider>(scope.ServiceProvider.GetRequiredService<IPaymentProvider>());
    }

    /// <summary>Sets process environment variables for the duration of the returned scope — this
    /// is what Program.cs's eager configuration reads actually see (see class remarks). Restores
    /// whatever was there before on dispose, so one test's settings never bleed into another.</summary>
    private static IDisposable ApplyEnvironment(IReadOnlyDictionary<string, string?> settings)
    {
        var previous = new Dictionary<string, string?>(settings.Count);
        foreach (var (name, value) in settings)
        {
            previous[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        return new RestoreEnvironment(previous);
    }

    private sealed class RestoreEnvironment(Dictionary<string, string?> previous) : IDisposable
    {
        public void Dispose()
        {
            foreach (var (name, value) in previous)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    /// <summary>Minimal hosting wraps a builder-time throw in host-startup exceptions; this walks
    /// down to the one Program.cs actually raised.</summary>
    private static Exception RootCause(Exception ex) => ex.InnerException is null ? ex : RootCause(ex.InnerException);
}

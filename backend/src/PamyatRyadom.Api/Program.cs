using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Data;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Services.Auth;
using PamyatRyadom.Api.Services.BurialSites;
using PamyatRyadom.Api.Services.Legal;
using PamyatRyadom.Api.Services.Media;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Postgres + EF Core, snake_case naming convention (mirrors NaidiAI so migrations/entities
// port across with minimal friction). Registration is skipped when no connection string is
// configured, so the app can still boot (e.g. `dotnet run` with nothing but appsettings) —
// every real environment (dev/test/prod) provides ConnectionStrings:DefaultConnection.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());
}

// ---------------------------------------------------------------------------------------------
// Identity / Auth module
// ---------------------------------------------------------------------------------------------

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));

// SMTP settings come from the environment (see .env.example) so a password never sits in appsettings.
builder.Services.Configure<EmailOptions>(options =>
{
    var configuration = builder.Configuration;
    options.Host = configuration["SMTP_HOST"] ?? options.Host;
    options.Port = configuration.GetValue<int?>("SMTP_PORT") ?? options.Port;
    options.UseSsl = configuration.GetValue<bool?>("SMTP_USE_SSL") ?? options.UseSsl;
    options.UserName = configuration["SMTP_USERNAME"] ?? options.UserName;
    options.Password = configuration["SMTP_PASSWORD"] ?? options.Password;
    options.FromEmail = configuration["SMTP_FROM_EMAIL"] ?? options.FromEmail;
    options.FromName = configuration["SMTP_FROM_NAME"] ?? options.FromName;
});

// Data Protection protects the TOTP secrets at rest (MfaService.ProtectSecret). The framework default
// key ring is per-container — `/root/.aspnet/DataProtection-Keys` on the container's writable layer,
// or an in-memory ring when no user profile exists — so it dies with the container: redeploy the api,
// or run a second replica, and every stored MFA secret becomes permanently undecryptable, locking
// every enrolled user out with no recovery path. Persist it to a directory expected to live on durable
// storage (a named Docker volume in compose) and pin the application name, since the ring is scoped by
// it and an unpinned name changes with the entry-assembly path.
var dataProtection = builder.Configuration.GetSection("DataProtection");
var dataProtectionApplicationName = dataProtection["ApplicationName"] is { Length: > 0 } configuredAppName
    ? configuredAppName
    : "PamyatRyadom.Api";

// Configurable per environment (DataProtection__KeyRingPath): the container mounts a volume, a
// developer running `dotnet run` gets a stable per-machine directory instead.
var configuredKeyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
var keyRingIsConfigured = !string.IsNullOrWhiteSpace(configuredKeyRingPath);
var keyRingPath = keyRingIsConfigured
    ? configuredKeyRingPath!.Trim()
    : Path.Combine(Path.GetTempPath(), "pamyat-ryadom", "dataprotection-keys");

// Created up front, and deliberately not swallowed: the key ring is written lazily, so an unwritable
// path would otherwise first surface as a failed MFA enrollment long after startup.
Directory.CreateDirectory(keyRingPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
    .SetApplicationName(dataProtectionApplicationName);

// Email delivery is chosen once, at startup, by environment: dev logs the code to the console, every
// other environment sends it over SMTP. A DI swap rather than a runtime flag, so no configuration
// value can turn production into "print the login code".
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<DevOtpInbox>();
    builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}

// Published legal documents: the single place their versions are declared, so an acceptance
// recorded today still identifies the exact text that was in force.
builder.Services.AddSingleton<ILegalDocumentRegistry, LegalDocumentRegistry>();

builder.Services.AddSingleton<IMfaService, MfaService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Burial sites: the client's own records, plus family access to them.
builder.Services.AddScoped<IBurialSiteService, BurialSiteService>();

// Media: private S3-compatible storage. Photographs are the product's core evidence, so the
// bytes live outside the database and are only ever reachable through a short-lived signed URL.
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.AddSingleton<IObjectStorage, S3ObjectStorage>();
builder.Services.AddScoped<IMediaService, MediaService>();

// Rate limiting: policies are registered per-endpoint as modules land.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Login-code requests: 5 per 10 minutes per (IP, destination) — enough for a user who mistypes
    // and retries, far too little to email-bomb an address or farm codes. The destination half of the
    // key is captured by middleware below, since a partition factory cannot read the request body.
    options.AddPolicy(AuthRateLimitPolicies.RequestOtp, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            AuthRateLimitPolicies.BuildOtpPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = AuthRateLimitPolicies.RequestOtpPermitLimit,
                Window = AuthRateLimitPolicies.RequestOtpWindow
            }));

    // Same envelope as every controller response, so a client has exactly one error shape to parse.
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            ApiResponse<object>.Fail(ApiError.Of("rate_limited", "Слишком много запросов. Попробуйте позже.")),
            token);
    };
});

// Model-binding failures are rejected by [ApiController] before an action runs, and would otherwise
// come back as ValidationProblemDetails — a second error shape the frontend would have to special-case.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .SelectMany(entry => entry.Value!.Errors.Select(error =>
                ApiError.Validation(
                    string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Некорректный запрос." : error.ErrorMessage,
                    new { field = entry.Key })))
            .ToArray();

        return new BadRequestObjectResult(ApiResponse<object>.Fail(errors));
    };
});

// CORS: frontend origin(s) come from config so dev/prod can point at different hosts without a
// rebuild. FRONTEND_ORIGIN accepts a comma-separated list; falls back to the local dev frontend.
const string FrontendCorsPolicy = "FrontendCors";
var frontendOrigins = (builder.Configuration["FRONTEND_ORIGIN"] ?? "http://localhost:3100")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy
            .WithOrigins(frontendOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// The temp-directory fallback is fine for a dev machine or a test run, but on a real deployment it is
// the same trap as the framework default — say so loudly rather than losing MFA secrets silently.
if (!keyRingIsConfigured && app.Environment.IsProduction())
{
    app.Logger.LogWarning(
        "DataProtection:KeyRingPath is not configured; using the non-durable fallback {KeyRingPath}. " +
        "Point DataProtection__KeyRingPath at persistent storage or stored MFA secrets will be lost on redeploy.",
        keyRingPath);
}

// Development convenience only: the MinIO container starts with no buckets, and having to create
// one by hand before the first photo upload is pure friction. A production bucket is provisioned
// deliberately — with its own access policy, lifecycle and retention — never by the app.
if (app.Environment.IsDevelopment())
{
    await app.Services.GetRequiredService<IObjectStorage>().EnsureBucketAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "PamyatRyadom API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors(FrontendCorsPolicy);

// Must run before UseRateLimiter: it buffers and peeks the OTP request body so the limiter can
// partition by destination as well as IP (see AuthRateLimitPolicies).
app.Use(async (context, next) =>
{
    await AuthRateLimitPolicies.CaptureOtpDestinationAsync(context);
    await next(context);
});

app.UseRateLimiter();

app.MapControllers();

// Development-only: hands back the last login code sent to an address, so the sign-in screen can
// show it under the input rather than making a developer read container logs.
//
// Mapped inside this branch on purpose. The route simply does not exist in any other environment —
// no feature flag, no header, no configuration value can bring it back, which is the only safe way
// to ship something that returns a login credential. Nothing here is a substitute for reading the
// code from a real inbox once SMTP is configured.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/v1/dev/last-otp", (string destination, DevOtpInbox inbox) =>
    {
        var code = inbox.Peek(destination);
        return code is null
            ? Results.NotFound(ApiResponse<object>.Fail(ApiError.NotFound("Код не найден.")))
            : Results.Ok(ApiResponse<object>.Ok(new { destination, code }));
    });
}

app.Run();

public partial class Program;

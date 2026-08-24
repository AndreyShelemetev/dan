using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Data;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Services.Auth;

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

// Protects the TOTP secrets at rest (MfaService). Idempotent — safe alongside the framework defaults.
builder.Services.AddDataProtection();

// Email delivery is chosen once, at startup, by environment: dev logs the code to the console, every
// other environment sends it over SMTP. A DI swap rather than a runtime flag, so no configuration
// value can turn production into "print the login code".
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}

builder.Services.AddSingleton<IMfaService, MfaService>();
builder.Services.AddScoped<IAuthService, AuthService>();

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

app.Run();

public partial class Program;

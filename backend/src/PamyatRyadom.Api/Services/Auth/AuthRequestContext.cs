using System.Net;

namespace PamyatRyadom.Api.Services.Auth;

public sealed record AuthRequestContext(IPAddress? IpAddress, string? UserAgent);

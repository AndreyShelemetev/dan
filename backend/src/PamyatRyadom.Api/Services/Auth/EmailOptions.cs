namespace PamyatRyadom.Api.Services.Auth;

/// <summary>SMTP configuration for <see cref="SmtpEmailSender"/>. Bound from SMTP_HOST / SMTP_PORT /
/// SMTP_USERNAME / SMTP_PASSWORD / SMTP_FROM_EMAIL (see Program.cs). Password is a secret — set it via
/// environment variable, never commit it.</summary>
public sealed class EmailOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;

    /// <summary>Implicit SSL on connect (true, e.g. port 465) vs STARTTLS (false, e.g. port 587).</summary>
    public bool UseSsl { get; set; } = true;

    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "PamyatRyadom";
    public int TimeoutSeconds { get; set; } = 20;
}

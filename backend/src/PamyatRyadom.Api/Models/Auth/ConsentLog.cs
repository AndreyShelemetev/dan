using System.Net;

namespace PamyatRyadom.Api.Models.Auth;

/// <summary>Records consent to processing of personal data, marketing contact, or cookies — guest-safe
/// (UserId is nullable: a lead/guest form submission can log consent before any account exists).
/// Terms-of-service acceptance is tracked separately via <see cref="LegalAcceptance"/> against a
/// versioned <see cref="LegalDocument"/>, not here.</summary>
public sealed class ConsentLog
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    /// <summary>Snapshot of the contact used at the time of consent — kept even if the user later
    /// changes their email/phone, since this is the legal record of what was agreed to and by whom.</summary>
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string ConsentType { get; set; } = ConsentTypes.PersonalData;
    public string DocumentVersion { get; set; } = string.Empty;
    public IPAddress IpAddress { get; set; } = IPAddress.None;
    public string? UserAgent { get; set; }
    public DateTimeOffset AcceptedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public User? User { get; set; }
}

public static class ConsentTypes
{
    public const string PersonalData = "personal_data";
    public const string Marketing = "marketing";
    public const string Cookies = "cookies";

    public static readonly IReadOnlyCollection<string> All = new[] { PersonalData, Marketing, Cookies };
}

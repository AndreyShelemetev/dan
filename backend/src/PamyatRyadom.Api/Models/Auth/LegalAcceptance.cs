using System.Net;

namespace PamyatRyadom.Api.Models.Auth;

/// <summary>Records that a specific person (or guest, before an account exists) accepted a specific
/// version of a <see cref="LegalDocument"/>. Append-only: a later disagreement is expressed as a new
/// row's absence plus <see cref="RevokedAt"/> on this one, never by editing or deleting the record.</summary>
public sealed class LegalAcceptance
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    public long DocumentId { get; set; }
    public IPAddress IpAddress { get; set; } = IPAddress.None;
    public string? UserAgent { get; set; }
    public DateTimeOffset AcceptedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public User? User { get; set; }
    public LegalDocument? Document { get; set; }
}

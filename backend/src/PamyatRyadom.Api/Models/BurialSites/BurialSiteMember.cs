namespace PamyatRyadom.Api.Models.BurialSites;

/// <summary>
/// Family access to one burial site. Several relatives commonly share responsibility for a
/// grave, and the product goal is that care does not depend on one person's memory — but
/// seeing a record and being able to spend money on it are deliberately separate rights.
/// </summary>
public sealed class BurialSiteMember
{
    public long Id { get; set; }
    public long BurialSiteId { get; set; }

    /// <summary>Null until the invitation is accepted: an invite is addressed to a contact,
    /// and the person may not have an account yet.</summary>
    public long? UserId { get; set; }

    /// <summary>Contact the invitation was sent to, kept as a snapshot so an unaccepted
    /// invite can still be listed and revoked.</summary>
    public string? InvitedContact { get; set; }

    public string Permission { get; set; } = BurialSitePermissions.View;

    public long InvitedByUserId { get; set; }

    /// <summary>SHA-256 of the invitation token. The token itself is never stored, matching
    /// how session and OTP secrets are handled elsewhere in this codebase.</summary>
    public string? InvitationTokenHash { get; set; }
    public DateTimeOffset? InvitationExpiresAt { get; set; }

    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public BurialSite? BurialSite { get; set; }

    /// <summary>An invitation grants nothing until it is accepted and while it is revoked.</summary>
    public bool IsActive => AcceptedAt is not null && RevokedAt is null;
}

public static class BurialSitePermissions
{
    /// <summary>Can see the record, its photos and the history of visits.</summary>
    public const string View = "view";

    /// <summary>Can additionally place orders against it — i.e. spend money. Granted
    /// explicitly by the owner, never by default on invitation.</summary>
    public const string Order = "order";

    /// <summary>Can additionally edit the record and manage other members.</summary>
    public const string Manage = "manage";

    public static readonly IReadOnlyCollection<string> All = new[] { View, Order, Manage };

    /// <summary>Permissions that allow placing an order.</summary>
    public static readonly IReadOnlyCollection<string> CanOrder = new[] { Order, Manage };
}

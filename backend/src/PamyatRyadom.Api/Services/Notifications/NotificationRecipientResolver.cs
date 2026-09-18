using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Data;

namespace PamyatRyadom.Api.Services.Notifications;

/// <summary>
/// Where a notification for a given user goes. <c>Order</c>/<c>Visit</c> only ever carry a bare
/// user id, and <c>User</c> itself has no email column — the address lives on
/// <c>AuthIdentity.Email</c>, because an account can be email- or phone-only. Same "prefer a
/// verified identity" choice <c>AuthService.MapUser</c>/<c>AdminUsersService</c> already make when
/// showing a user their own contact, factored out once so the four notification call sites don't
/// each repeat the query.
/// </summary>
public static class NotificationRecipientResolver
{
    /// <summary>Returns <c>null</c> for a phone-only account: SMS login itself is not wired up
    /// yet, so there is nowhere to send a notification.</summary>
    public static Task<string?> ResolveEmailAsync(AppDbContext db, long userId, CancellationToken ct = default) =>
        db.AuthIdentities
            .Where(x => x.UserId == userId && x.Email != null)
            .OrderByDescending(x => x.IsVerified)
            .Select(x => x.Email)
            .FirstOrDefaultAsync(ct);
}

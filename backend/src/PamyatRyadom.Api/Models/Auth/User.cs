namespace PamyatRyadom.Api.Models.Auth;

public sealed class User
{
    public long Id { get; set; }
    public string Role { get; set; } = UserRoles.Client;
    public string Status { get; set; } = UserStatuses.Active;
    public string? DisplayName { get; set; }
    public string? Locale { get; set; }
    public string? Timezone { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<AuthIdentity> AuthIdentities { get; set; } = new List<AuthIdentity>();
    public ICollection<AuthSession> AuthSessions { get; set; } = new List<AuthSession>();
    public MfaSecret? MfaSecret { get; set; }
}

public static class UserRoles
{
    public const string Client = "client";
    public const string Executor = "executor";
    public const string Dispatcher = "dispatcher";
    public const string Qa = "qa";
    public const string Support = "support";
    public const string Finance = "finance";
    public const string Admin = "admin";
    public const string Superadmin = "superadmin";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        Client, Executor, Dispatcher, Qa, Support, Finance, Admin, Superadmin
    };

    /// <summary>Internal/operational and admin roles. Once an account in one of these roles has
    /// enrolled MFA, <see cref="Services.Auth.RequireRoleFilter"/> requires an MFA-elevated
    /// (<see cref="AuthSession.IsPrivileged"/>) session to pass role checks.</summary>
    public static readonly IReadOnlyCollection<string> Privileged = new[]
    {
        Dispatcher, Qa, Support, Finance, Admin, Superadmin
    };
}

public static class UserStatuses
{
    public const string Active = "active";
    public const string Blocked = "blocked";
    public const string Deleted = "deleted";

    public static readonly IReadOnlyCollection<string> All = new[] { Active, Blocked, Deleted };
}

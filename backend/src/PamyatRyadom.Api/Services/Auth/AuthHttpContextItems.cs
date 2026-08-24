namespace PamyatRyadom.Api.Services.Auth;

/// <summary>Keys under which <see cref="RequireRoleFilter"/> hands the resolved session to the action
/// (read through AuthorizedControllerBase, never by string literal).</summary>
internal static class AuthHttpContextItems
{
    public const string User = "PamyatRyadom.Auth.User";

    /// <summary>The session token that is valid *now* — which is not the cookie the request arrived
    /// with if the filter rotated it.</summary>
    public const string SessionToken = "PamyatRyadom.Auth.SessionToken";

    public const string MfaSetupRequired = "PamyatRyadom.Auth.MfaSetupRequired";
}

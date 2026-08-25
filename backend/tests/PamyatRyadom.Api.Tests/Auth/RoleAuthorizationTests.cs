using System.Net;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests.Auth;

/// <summary>
/// <c>RequireRoleFilter</c> through real HTTP: no session is 401, the wrong role is 403, an allowed
/// role is 200, and a privileged account that still owes MFA enrollment is let through carrying the
/// <c>meta.mfaSetupRequired</c> signal (a hard block would lock out the first administrator, who has
/// no way to enroll before signing in).
///
/// The narrowed role set lives on <see cref="RoleProbeController"/> in this assembly — every
/// production endpoint today is <c>[RequireRole]</c> with no arguments.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RoleAuthorizationTests : AuthIntegrationTest
{
    private const string AnyRolePath = "/api/v1/test/role-probe/any";
    private const string DispatchPath = "/api/v1/test/role-probe/dispatch";
    private const string AnyWithMetaPath = "/api/v1/test/role-probe/any-with-meta";

    private const string Email = "roles@example.com";

    public RoleAuthorizationTests(PostgresFixture postgres)
        : base(postgres)
    {
    }

    [Theory]
    [InlineData(AnyRolePath)]
    [InlineData(DispatchPath)]
    public async Task Without_a_session_the_answer_is_401(string path)
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
        Assert.Equal("unauthorized", response.ErrorCode);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task A_role_outside_the_allowed_set_is_403()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);

        // OTP registration always creates a client, which the dispatch desk does not admit.
        var response = await client.GetAsync(DispatchPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        Assert.Equal("forbidden", response.ErrorCode);
        Assert.Null(response.Data);
    }

    [Theory]
    [InlineData(UserRoles.Dispatcher)]
    [InlineData(UserRoles.Admin)]
    public async Task An_allowed_role_is_200(string role)
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        await SetRoleAsync(factory, role);

        var response = await client.GetAsync(DispatchPath);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(role, response.Data?["role"]?.GetValue<string>());
    }

    [Fact]
    public async Task RequireRole_without_arguments_admits_any_signed_in_role()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);

        var asClient = await client.GetAsync(AnyRolePath);
        Assert.Equal(HttpStatusCode.OK, asClient.Status);
        Assert.Equal(UserRoles.Client, asClient.Data?["role"]?.GetValue<string>());

        await SetRoleAsync(factory, UserRoles.Executor);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(AnyRolePath)).Status);
    }

    [Fact]
    public async Task A_privileged_role_without_mfa_is_admitted_with_the_setup_signal()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        await SetRoleAsync(factory, UserRoles.Dispatcher);

        var response = await client.GetAsync(AnyRolePath);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.True(response.Meta?["mfaSetupRequired"]?.GetValue<bool>());
    }

    [Fact]
    public async Task Me_carries_the_same_setup_signal()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        await SetRoleAsync(factory, UserRoles.Superadmin);

        var response = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.True(response.Meta?["mfaSetupRequired"]?.GetValue<bool>());
    }

    [Theory]
    [InlineData(UserRoles.Client)]
    [InlineData(UserRoles.Executor)]
    public async Task An_unprivileged_role_gets_no_setup_signal(string role)
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        await SetRoleAsync(factory, role);

        var response = await client.GetAsync(AnyRolePath);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Null(response.Meta);
    }

    [Fact]
    public async Task The_setup_signal_is_merged_into_the_actions_own_meta()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        await SetRoleAsync(factory, UserRoles.Finance);

        var response = await client.GetAsync(AnyWithMetaPath);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.True(response.Meta?["mfaSetupRequired"]?.GetValue<bool>());
        Assert.Equal("value", response.Meta?["probe"]?.GetValue<string>());
    }

    [Fact]
    public async Task The_setup_signal_disappears_once_mfa_is_enrolled()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        await SetRoleAsync(factory, UserRoles.Dispatcher);

        var enroll = await client.PostAsync(MfaEnrollPath);
        Assert.Equal(HttpStatusCode.OK, enroll.Status);

        var secret = enroll.Data?["secret"]?.GetValue<string>();
        Assert.NotNull(secret);

        var verify = await client.PostAsync(MfaVerifyPath, new { code = TestTotp.Now(secret!) });
        Assert.Equal(HttpStatusCode.OK, verify.Status);

        var response = await client.GetAsync(AnyRolePath);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Null(response.Meta);
        Assert.True(response.Data?["mfaEnabled"]?.GetValue<bool>());
    }

    [Fact]
    public async Task A_blocked_privileged_account_is_turned_away()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, Email);
        await SetRoleAsync(factory, UserRoles.Admin);
        await SetUserStatusAsync(factory, UserStatuses.Blocked);

        var response = await client.GetAsync(DispatchPath);

        // The session is revoked before the role check is ever reached, so this reads as 401 rather
        // than the filter's own 403-for-inactive branch.
        Assert.Equal(HttpStatusCode.Unauthorized, response.Status);
        Assert.NotNull((await SingleSessionAsync(factory)).RevokedAt);
    }
}

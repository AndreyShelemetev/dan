using System.Net;
using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Tests.Infrastructure;

namespace PamyatRyadom.Api.Tests.Auth;

/// <summary>
/// Admin user administration — the only way to give someone a staff or executor account outside
/// Development, where <c>DevAccountSeeder</c> does the job instead.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AdminUsersTests : AuthIntegrationTest
{
    private const string AdminEmail = "admin-users-admin@example.com";
    private const string DispatcherEmail = "admin-users-dispatcher@example.com";
    private const string ExecutorEmail = "admin-users-new-executor@example.com";

    private const string UsersPath = "/api/v1/admin/users";

    public AdminUsersTests(PostgresFixture postgres) : base(postgres) { }

    [Fact]
    public async Task A_dispatcher_is_refused()
    {
        var factory = CreateFactory();
        var client = factory.CreateApiClient();

        await LoginAsync(factory, client, DispatcherEmail);
        await SetRoleAsync(factory, DispatcherEmail, UserRoles.Dispatcher);

        var list = await client.GetAsync(UsersPath);
        Assert.Equal(HttpStatusCode.Forbidden, list.Status);

        var create = await client.PostAsync(UsersPath, new { email = ExecutorEmail, role = UserRoles.Executor });
        Assert.Equal(HttpStatusCode.Forbidden, create.Status);
    }

    [Fact]
    public async Task An_admin_creates_an_executor_account()
    {
        var factory = CreateFactory();
        var admin = factory.CreateApiClient();

        await LoginAsync(factory, admin, AdminEmail);
        await SetRoleAsync(factory, AdminEmail, UserRoles.Admin);

        var created = await admin.PostAsync(UsersPath, new
        {
            email = ExecutorEmail,
            role = UserRoles.Executor,
            displayName = "Иван Исполнителев"
        });

        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal(UserRoles.Executor, created.Data?["role"]?.GetValue<string>());
        Assert.Equal(UserStatuses.Active, created.Data?["status"]?.GetValue<string>());

        var list = await admin.GetAsync($"{UsersPath}?role={UserRoles.Executor}");
        Assert.Equal(HttpStatusCode.OK, list.Status);
        Assert.Equal(ExecutorEmail, list.Data?.AsArray()[0]?["email"]?.GetValue<string>());

        // The new account is not a dev shortcut into a session — it can only ever sign in through
        // the same OTP flow everyone else uses.
        var code = await RequestCodeAsync(factory, factory.CreateApiClient(), ExecutorEmail);
        Assert.False(string.IsNullOrWhiteSpace(code));
    }

    [Fact]
    public async Task Creating_a_duplicate_email_is_a_conflict()
    {
        var factory = CreateFactory();
        var admin = factory.CreateApiClient();

        await LoginAsync(factory, admin, AdminEmail);
        await SetRoleAsync(factory, AdminEmail, UserRoles.Admin);

        var first = await admin.PostAsync(UsersPath, new { email = ExecutorEmail, role = UserRoles.Executor });
        Assert.Equal(HttpStatusCode.Created, first.Status);

        var second = await admin.PostAsync(UsersPath, new { email = ExecutorEmail, role = UserRoles.Dispatcher });
        Assert.Equal(HttpStatusCode.Conflict, second.Status);
        Assert.Equal("email_taken", second.ErrorCode);
    }

    [Fact]
    public async Task Deactivating_a_user_revokes_their_sessions_and_blocks_me()
    {
        var factory = CreateFactory();
        var admin = factory.CreateApiClient();
        var target = factory.CreateApiClient();

        await LoginAsync(factory, admin, AdminEmail);
        await SetRoleAsync(factory, AdminEmail, UserRoles.Admin);

        var targetId = await LoginAsAsync(factory, target, ExecutorEmail, UserRoles.Executor);

        // The target is signed in and can read their own session before deactivation.
        Assert.Equal(HttpStatusCode.OK, (await target.GetAsync(MePath)).Status);

        var deactivated = await admin.PostAsync($"{UsersPath}/{targetId}/deactivate");
        Assert.Equal(HttpStatusCode.OK, deactivated.Status);
        Assert.Equal(UserStatuses.Blocked, deactivated.Data?["status"]?.GetValue<string>());

        // The session that was live before deactivation is now revoked, not merely rejected on the
        // status check — GET /auth/me with the same cookie comes back 401.
        var meAfter = await target.GetAsync(MePath);
        Assert.Equal(HttpStatusCode.Unauthorized, meAfter.Status);

        var reactivated = await admin.PostAsync($"{UsersPath}/{targetId}/activate");
        Assert.Equal(HttpStatusCode.OK, reactivated.Status);
        Assert.Equal(UserStatuses.Active, reactivated.Data?["status"]?.GetValue<string>());
    }

    [Fact]
    public async Task An_admin_cannot_deactivate_their_own_account()
    {
        var factory = CreateFactory();
        var admin = factory.CreateApiClient();

        var adminId = await LoginAsAsync(factory, admin, AdminEmail, UserRoles.Admin);

        var response = await admin.PostAsync($"{UsersPath}/{adminId}/deactivate");

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal(UserStatuses.Active, (await SingleUserAsync(factory)).Status);
    }

    [Fact]
    public async Task Changing_a_role_is_audited_with_identifiers_only()
    {
        var factory = CreateFactory();
        var admin = factory.CreateApiClient();

        await LoginAsync(factory, admin, AdminEmail);
        await SetRoleAsync(factory, AdminEmail, UserRoles.Admin);

        var targetId = await LoginAsAsync(factory, factory.CreateApiClient(), ExecutorEmail, UserRoles.Executor);

        var response = await admin.SendAsync(
            HttpMethod.Patch, $"{UsersPath}/{targetId}/role", new { role = UserRoles.Dispatcher });

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal(UserRoles.Dispatcher, response.Data?["role"]?.GetValue<string>());

        var metadata = await LatestAuditMetadataAsync(factory, SecurityAuditEventTypes.RoleChanged);
        Assert.Equal(UserRoles.Executor, metadata?["from"]?.GetValue<string>());
        Assert.Equal(UserRoles.Dispatcher, metadata?["to"]?.GetValue<string>());
    }

    /// <summary>Logs an account in through the real OTP flow and promotes it to <paramref name="role"/>,
    /// returning its id — the only way to get a second, non-client account into the same database as
    /// the admin under test.</summary>
    private static async Task<long> LoginAsAsync(PamyatApiFactory factory, ApiClient client, string email, string role)
    {
        await LoginAsync(factory, client, email);
        return await SetRoleAsync(factory, email, role);
    }

    private static async Task<long> SetRoleAsync(PamyatApiFactory factory, string email, string role)
    {
        long id = 0;

        await factory.WithDbAsync(async db =>
        {
            var identity = await db.AuthIdentities.Include(i => i.User).FirstAsync(i => i.Email == email);
            identity.User!.Role = role;
            id = identity.User.Id;
            await db.SaveChangesAsync();
        });

        return id;
    }
}

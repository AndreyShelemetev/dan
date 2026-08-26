using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Data;
using PamyatRyadom.Api.Models.Auth;

namespace PamyatRyadom.Api.Services.Dev;

/// <summary>
/// Test accounts, one per role, so a role-specific screen can be opened without editing the
/// database by hand.
///
/// Development only, and structurally so: this is registered and invoked inside the
/// <c>IsDevelopment()</c> branch in Program.cs. It is not gated by a configuration value, because
/// a flag that seeds a ready-made admin account into production is exactly the kind of thing
/// that gets switched on by accident.
///
/// The accounts carry no password and no session — they are reached through the normal
/// email-code flow, and in development the code appears under the sign-in field. So an account
/// existing here grants nothing on its own.
/// </summary>
public interface IDevAccountSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}

public sealed class DevAccountSeeder : IDevAccountSeeder
{
    /// <summary>Email is the login. Chosen to be readable in a log rather than realistic.</summary>
    public static readonly IReadOnlyList<(string Email, string Role, string DisplayName)> Accounts = new[]
    {
        ("client@pamyat.test", UserRoles.Client, "Тестовый клиент"),
        ("executor@pamyat.test", UserRoles.Executor, "Тестовый исполнитель"),
        ("dispatcher@pamyat.test", UserRoles.Dispatcher, "Тестовый диспетчер"),
        ("qa@pamyat.test", UserRoles.Qa, "Тестовый QA"),
        ("support@pamyat.test", UserRoles.Support, "Тестовая поддержка"),
        ("finance@pamyat.test", UserRoles.Finance, "Тестовые финансы"),
        ("admin@pamyat.test", UserRoles.Admin, "Тестовый администратор"),
    };

    private readonly AppDbContext _db;
    private readonly ILogger<DevAccountSeeder> _logger;

    public DevAccountSeeder(AppDbContext db, ILogger<DevAccountSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        foreach (var (email, role, displayName) in Accounts)
        {
            var existing = await _db.AuthIdentities
                .Include(i => i.User)
                .FirstOrDefaultAsync(i => i.Provider == AuthProviders.Email && i.Email == email, ct);

            if (existing is not null)
            {
                // Keep the role in step when this list changes, but never resurrect an account
                // someone deliberately blocked while testing.
                if (existing.User is not null && existing.User.Role != role && existing.User.Status == UserStatuses.Active)
                {
                    existing.User.Role = role;
                }

                continue;
            }

            var user = new User
            {
                Role = role,
                Status = UserStatuses.Active,
                DisplayName = displayName,
                Locale = "ru",
            };

            _db.AuthIdentities.Add(new AuthIdentity
            {
                User = user,
                Provider = AuthProviders.Email,
                Email = email,
                // Verified: the point is to skip the ceremony, and the mailbox does not exist.
                IsVerified = true,
            });

            _logger.LogInformation("Seeded dev account {Email} with role {Role}", email, role);
        }

        if (_db.ChangeTracker.HasChanges())
        {
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                _db.ChangeTracker.Clear();
            }
        }
    }
}

using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PamyatRyadom.Api.Data;
using PamyatRyadom.Api.Dtos.Auth;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Services.Common;

namespace PamyatRyadom.Api.Services.Auth;

public interface IAdminUsersService
{
    Task<ServiceResult<IReadOnlyList<AdminUserDto>>> ListAsync(string? role, CancellationToken ct = default);

    Task<ServiceResult<AdminUserDto>> CreateAsync(
        long actorId, string actorRole, CreateAdminUserDto dto, AuthRequestContext context, CancellationToken ct = default);

    Task<ServiceResult<AdminUserDto>> ChangeRoleAsync(
        long actorId, string actorRole, long userId, ChangeUserRoleDto dto, AuthRequestContext context, CancellationToken ct = default);

    Task<ServiceResult<AdminUserDto>> DeactivateAsync(
        long actorId, string actorRole, long userId, AuthRequestContext context, CancellationToken ct = default);

    Task<ServiceResult<AdminUserDto>> ActivateAsync(
        long actorId, string actorRole, long userId, AuthRequestContext context, CancellationToken ct = default);
}

/// <summary>
/// Administration of user accounts outside Development, where <c>DevAccountSeeder</c> is not
/// available: the only way to give someone a <c>dispatcher</c>/<c>qa</c>/<c>executor</c>/... account
/// today, since OTP self-registration always creates a <c>client</c>.
///
/// Every mutation is audited by identifiers and event type only — no PII in the metadata, per
/// CLAUDE.md's logging rule.
/// </summary>
public sealed class AdminUsersService : IAdminUsersService
{
    private readonly AppDbContext _db;

    public AdminUsersService(AppDbContext db) => _db = db;

    public async Task<ServiceResult<IReadOnlyList<AdminUserDto>>> ListAsync(string? role, CancellationToken ct = default)
    {
        var normalizedRole = Clean(role)?.ToLowerInvariant();
        if (normalizedRole is not null && !UserRoles.All.Contains(normalizedRole))
        {
            return ServiceResult<IReadOnlyList<AdminUserDto>>.Validation(
                "Недопустимая роль.", new { field = "role" });
        }

        var query = _db.Users
            .Include(u => u.AuthIdentities)
            .Include(u => u.MfaSecret)
            .AsQueryable();

        if (normalizedRole is not null)
        {
            query = query.Where(u => u.Role == normalizedRole);
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<AdminUserDto>>.Ok(users.Select(Map).ToList());
    }

    public async Task<ServiceResult<AdminUserDto>> CreateAsync(
        long actorId, string actorRole, CreateAdminUserDto dto, AuthRequestContext context, CancellationToken ct = default)
    {
        var email = NormalizeEmail(dto.Email);
        if (email is null || email.Length > 320 || !IsValidEmail(email))
        {
            return ServiceResult<AdminUserDto>.Validation("Укажите корректный email.", new { field = "email" });
        }

        var role = Clean(dto.Role)?.ToLowerInvariant();
        if (role is null || !UserRoles.All.Contains(role))
        {
            return ServiceResult<AdminUserDto>.Validation("Недопустимая роль.", new { field = "role" });
        }

        var displayName = Clean(dto.DisplayName);
        if (displayName is { Length: > 255 })
        {
            return ServiceResult<AdminUserDto>.Validation(
                "Имя не должно превышать 255 символов.", new { field = "displayName" });
        }

        var exists = await _db.AuthIdentities
            .AnyAsync(x => x.Provider == AuthProviders.Email && x.Email == email, ct);
        if (exists)
        {
            return ServiceResult<AdminUserDto>.Fail(
                StatusCodes.Status409Conflict,
                ApiError.Of("email_taken", "Аккаунт с таким email уже существует."));
        }

        var user = new User
        {
            Role = role,
            Status = UserStatuses.Active,
            DisplayName = displayName,
            Locale = "ru"
        };

        user.AuthIdentities.Add(new AuthIdentity
        {
            User = user,
            Provider = AuthProviders.Email,
            Email = email,
            IsVerified = false
        });

        _db.Users.Add(user);

        AddAuditLog(SecurityAuditEventTypes.UserCreated, actorId, actorRole, context, new { target_user_role = role });

        await _db.SaveChangesAsync(ct);

        return ServiceResult<AdminUserDto>.Created(Map(user));
    }

    public async Task<ServiceResult<AdminUserDto>> ChangeRoleAsync(
        long actorId, string actorRole, long userId, ChangeUserRoleDto dto, AuthRequestContext context, CancellationToken ct = default)
    {
        var role = Clean(dto.Role)?.ToLowerInvariant();
        if (role is null || !UserRoles.All.Contains(role))
        {
            return ServiceResult<AdminUserDto>.Validation("Недопустимая роль.", new { field = "role" });
        }

        var user = await LoadUserAsync(userId, ct);
        if (user is null)
        {
            return ServiceResult<AdminUserDto>.NotFound("Пользователь не найден.");
        }

        var previousRole = user.Role;
        if (previousRole != role)
        {
            user.Role = role;
            AddAuditLog(
                SecurityAuditEventTypes.RoleChanged,
                actorId,
                actorRole,
                context,
                new { target_user_id = user.Id, from = previousRole, to = role });

            await _db.SaveChangesAsync(ct);
        }

        return ServiceResult<AdminUserDto>.Ok(Map(user));
    }

    public Task<ServiceResult<AdminUserDto>> DeactivateAsync(
        long actorId, string actorRole, long userId, AuthRequestContext context, CancellationToken ct = default) =>
        SetStatusAsync(actorId, actorRole, userId, UserStatuses.Blocked, context, ct);

    public Task<ServiceResult<AdminUserDto>> ActivateAsync(
        long actorId, string actorRole, long userId, AuthRequestContext context, CancellationToken ct = default) =>
        SetStatusAsync(actorId, actorRole, userId, UserStatuses.Active, context, ct);

    private async Task<ServiceResult<AdminUserDto>> SetStatusAsync(
        long actorId, string actorRole, long userId, string status, AuthRequestContext context, CancellationToken ct)
    {
        if (status == UserStatuses.Blocked && userId == actorId)
        {
            // Locking out the only account with the credentials to undo it is not a status change
            // anyone means to make from this endpoint.
            return ServiceResult<AdminUserDto>.Validation("Нельзя деактивировать свою учётную запись.");
        }

        var user = await LoadUserAsync(userId, ct);
        if (user is null)
        {
            return ServiceResult<AdminUserDto>.NotFound("Пользователь не найден.");
        }

        var previousStatus = user.Status;
        if (previousStatus != status)
        {
            user.Status = status;

            AddAuditLog(
                SecurityAuditEventTypes.UserStatusChanged,
                actorId,
                actorRole,
                context,
                new { target_user_id = user.Id, from = previousStatus, to = status });

            if (status != UserStatuses.Active)
            {
                var now = DateTimeOffset.UtcNow;
                var sessions = await _db.AuthSessions
                    .Where(s => s.UserId == user.Id && s.RevokedAt == null && s.ExpiresAt > now)
                    .ToListAsync(ct);

                foreach (var session in sessions)
                {
                    session.RevokedAt = now;
                }

                if (sessions.Count > 0)
                {
                    AddAuditLog(
                        SecurityAuditEventTypes.SessionRevoked,
                        actorId,
                        actorRole,
                        context,
                        new { target_user_id = user.Id, revoked_count = sessions.Count, scope = "deactivation" });
                }
            }

            await _db.SaveChangesAsync(ct);
        }

        return ServiceResult<AdminUserDto>.Ok(Map(user));
    }

    private Task<User?> LoadUserAsync(long userId, CancellationToken ct) =>
        _db.Users
            .Include(u => u.AuthIdentities)
            .Include(u => u.MfaSecret)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

    private void AddAuditLog(string eventType, long actorId, string actorRole, AuthRequestContext context, object metadata)
    {
        _db.SecurityAuditLogs.Add(new SecurityAuditLog
        {
            UserId = actorId,
            EventType = eventType,
            ActorRole = actorRole,
            IpAddress = context.IpAddress,
            UserAgent = context.UserAgent,
            Metadata = JsonSerializer.SerializeToDocument(metadata)
        });
    }

    private static AdminUserDto Map(User user)
    {
        var email = user.AuthIdentities
            .Where(x => x.Email != null)
            .OrderByDescending(x => x.IsVerified)
            .Select(x => x.Email)
            .FirstOrDefault();

        var phone = user.AuthIdentities
            .Where(x => x.Phone != null)
            .OrderByDescending(x => x.IsVerified)
            .Select(x => x.Phone)
            .FirstOrDefault();

        return new AdminUserDto
        {
            Id = user.Id,
            Role = user.Role,
            Status = user.Status,
            DisplayName = user.DisplayName,
            Email = email,
            Phone = phone,
            MfaEnabled = user.MfaSecret?.EnabledAt is not null,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt
        };
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string? NormalizeEmail(string? email) => Clean(email)?.ToLowerInvariant();

    private static bool IsValidEmail(string email)
    {
        try
        {
            var parsed = new MailAddress(email);
            return string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

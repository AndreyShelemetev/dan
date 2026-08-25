using System.Globalization;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PamyatRyadom.Api.Data;
using PamyatRyadom.Api.Dtos.Auth;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Models.Auth;

namespace PamyatRyadom.Api.Services.Auth;

/// <summary>Passwordless (OTP) login, opaque session lifecycle, and TOTP enrollment.
///
/// Three deliberate differences from the NaidiAI implementation this is ported from:
/// (1) session tokens ROTATE on use instead of living unchanged for the whole TTL;
/// (2) session TTL depends on the account's role — privileged roles get hours, not weeks;
/// (3) there is no "return the code in the API response" development flag — the dev channel is
///     <see cref="ConsoleEmailSender"/>, chosen by DI, so no runtime setting can leak a code.
/// </summary>
public sealed class AuthService : IAuthService
{
    /// <summary>Version stamped on the consent/acceptance records written at registration. Real legal
    /// versioning (published documents, re-consent on a new version) is a separate task.</summary>
    private const string PlaceholderDocumentVersion = "v0-draft";

    /// <summary>The document a client accepts at registration. Executors are onboarded through a
    /// different flow and accept <see cref="LegalDocumentTypes.OfertaExecutor"/> there.</summary>
    private const string RegistrationTermsDocumentType = LegalDocumentTypes.OfertaClient;

    private const int UserAgentMaxLength = 512;

    /// <summary>Metadata reason stamped on the <c>session_revoked</c> audit row when a session is killed
    /// by its absolute lifetime wall, as opposed to a user-initiated logout / "sign out everywhere".
    /// It rides in the metadata rather than a new event type so the <c>ck_security_audit_logs_event_type</c>
    /// CHECK constraint (and the schema behind it) stays untouched.</summary>
    private const string MaxLifetimeExceededReason = "max_lifetime_exceeded";

    private readonly AppDbContext _db;
    private readonly AuthOptions _options;
    private readonly IEmailSender _emailSender;
    private readonly IMfaService _mfa;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext db,
        IOptions<AuthOptions> options,
        IEmailSender emailSender,
        IMfaService mfa,
        IHostEnvironment environment,
        ILogger<AuthService> logger)
    {
        _db = db;
        _options = options.Value;
        _emailSender = emailSender;
        _mfa = mfa;
        _environment = environment;
        _logger = logger;
    }

    // ---------------------------------------------------------------------------------------------
    // OTP request
    // ---------------------------------------------------------------------------------------------

    public async Task<AuthServiceResult<AuthMessageDto>> RequestOtpAsync(
        string? destination,
        string? channel,
        string? purpose,
        AuthRequestContext context,
        CancellationToken ct = default)
    {
        var normalized = NormalizeRequest(destination, channel, purpose, out var errors);
        if (errors.Count > 0)
        {
            return AuthServiceResult<AuthMessageDto>.Fail(StatusCodes.Status400BadRequest, errors.ToArray());
        }

        if (normalized.Channel == OtpChannels.Sms)
        {
            // No SMS provider is wired up yet; fail loudly rather than silently dropping a login attempt.
            return AuthServiceResult<AuthMessageDto>.Fail(
                StatusCodes.Status501NotImplemented,
                ApiError.Of("channel_not_supported", "Вход по SMS пока недоступен. Используйте email."));
        }

        var now = DateTimeOffset.UtcNow;

        // One live code per destination+purpose: requesting a new one invalidates the previous, so a
        // code snooped from an old email can't be used after the user asks for a fresh one.
        var previous = await _db.OtpCodes
            .Where(x => x.Destination == normalized.Destination
                        && x.Purpose == normalized.Purpose
                        && x.ConsumedAt == null)
            .ToListAsync(ct);

        foreach (var stale in previous)
        {
            stale.ConsumedAt = now;
        }

        var code = GenerateNumericCode();

        _db.OtpCodes.Add(new OtpCode
        {
            Channel = normalized.Channel,
            Destination = normalized.Destination,
            Purpose = normalized.Purpose,
            CodeHash = SecretHasher.Hash(BuildCodePayload(normalized.Destination, normalized.Purpose, code)),
            ExpiresAt = now.AddMinutes(_options.CodeTtlMinutes),
            AttemptCount = 0,
            IpAddress = context.IpAddress,
            UserAgent = Truncate(context.UserAgent, UserAgentMaxLength)
        });

        // No user id and no destination in the metadata: this row is written before we know (or care)
        // whether an account exists, and the destination is PII.
        AddAuditLog(
            SecurityAuditEventTypes.OtpRequested,
            context,
            new { channel = normalized.Channel, purpose = normalized.Purpose, destination_key = DestinationKey(normalized.Destination) });

        await _db.SaveChangesAsync(ct);

        var sent = await TrySendCodeAsync(normalized.Destination, code, ct);
        if (!sent && !_environment.IsDevelopment())
        {
            return AuthServiceResult<AuthMessageDto>.Fail(
                StatusCodes.Status502BadGateway,
                ApiError.Of("delivery_failed", "Не удалось отправить код. Попробуйте позже."));
        }

        // Identical response whether or not an account exists — this endpoint must not be usable to
        // enumerate registered users.
        return AuthServiceResult<AuthMessageDto>.Ok(new AuthMessageDto { Message = "Код отправлен" });
    }

    // ---------------------------------------------------------------------------------------------
    // OTP verification / login
    // ---------------------------------------------------------------------------------------------

    public async Task<AuthServiceResult<VerifiedOtpResult>> VerifyOtpAsync(
        string? destination,
        string? channel,
        string? purpose,
        string? code,
        AuthRequestContext context,
        CancellationToken ct = default)
    {
        var normalized = NormalizeRequest(destination, channel, purpose, out var errors);
        var trimmedCode = Clean(code);
        if (trimmedCode is null || trimmedCode.Length != 6 || trimmedCode.Any(ch => ch is < '0' or > '9'))
        {
            errors.Add(ApiError.Validation("Код должен состоять из 6 цифр.", new { field = "code" }));
        }

        if (errors.Count > 0)
        {
            return AuthServiceResult<VerifiedOtpResult>.Fail(StatusCodes.Status400BadRequest, errors.ToArray());
        }

        var now = DateTimeOffset.UtcNow;
        var otp = await _db.OtpCodes
            .Where(x => x.Destination == normalized.Destination
                        && x.Purpose == normalized.Purpose
                        && x.ConsumedAt == null
                        && x.ExpiresAt > now)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

        if (otp is null)
        {
            AddAuditLog(SecurityAuditEventTypes.LoginFailed, context, new { reason = "no_active_code" });
            await _db.SaveChangesAsync(ct);

            return AuthServiceResult<VerifiedOtpResult>.Fail(
                StatusCodes.Status400BadRequest,
                ApiError.Of("invalid_code", "Код не найден или истёк."));
        }

        if (otp.AttemptCount >= _options.MaxCodeAttempts)
        {
            AddAuditLog(SecurityAuditEventTypes.LoginFailed, context, new { reason = "too_many_attempts", otp_code_id = otp.Id });
            await _db.SaveChangesAsync(ct);

            return AuthServiceResult<VerifiedOtpResult>.Fail(
                StatusCodes.Status429TooManyRequests,
                ApiError.Of("too_many_attempts", "Слишком много попыток. Запросите новый код."));
        }

        if (!SecretHasher.Verify(otp.CodeHash, BuildCodePayload(normalized.Destination, normalized.Purpose, trimmedCode!)))
        {
            otp.AttemptCount += 1;
            var exhausted = otp.AttemptCount >= _options.MaxCodeAttempts;

            AddAuditLog(
                SecurityAuditEventTypes.LoginFailed,
                context,
                new { reason = exhausted ? "too_many_attempts" : "invalid_code", otp_code_id = otp.Id });
            await _db.SaveChangesAsync(ct);

            return exhausted
                ? AuthServiceResult<VerifiedOtpResult>.Fail(
                    StatusCodes.Status429TooManyRequests,
                    ApiError.Of("too_many_attempts", "Слишком много попыток. Запросите новый код."))
                : AuthServiceResult<VerifiedOtpResult>.Fail(
                    StatusCodes.Status400BadRequest,
                    ApiError.Of("invalid_code", "Неверный код."));
        }

        var identity = await FindIdentityAsync(normalized, ct);
        var user = identity?.User;
        var isNewUser = user is null;

        if (isNewUser)
        {
            // Reference data, so it is ensured (and committed) before the registration write below —
            // keeping it out of that transaction means a concurrent first registration can't turn a
            // unique-index conflict on the placeholder document into a failed login.
            var termsDocumentId = await EnsurePlaceholderLegalDocumentAsync(RegistrationTermsDocumentType, ct);

            user = new User
            {
                Role = UserRoles.Client,
                Status = UserStatuses.Active,
                Locale = "ru"
            };

            identity = new AuthIdentity
            {
                User = user,
                Provider = normalized.Provider,
                Email = normalized.Channel == OtpChannels.Email ? normalized.Destination : null,
                Phone = normalized.Channel == OtpChannels.Sms ? normalized.Destination : null,
                IsVerified = true
            };

            user.AuthIdentities.Add(identity);
            _db.Users.Add(user);
            AddRegistrationConsent(user, identity, context, termsDocumentId);
        }

        if (user!.Status != UserStatuses.Active)
        {
            AddAuditLog(SecurityAuditEventTypes.LoginFailed, context, new { reason = "user_not_active" }, user);
            await _db.SaveChangesAsync(ct);

            return AuthServiceResult<VerifiedOtpResult>.Fail(
                StatusCodes.Status403Forbidden,
                ApiError.Of("forbidden", "Учётная запись недоступна."));
        }

        otp.ConsumedAt = now;
        identity!.IsVerified = true;
        identity.LastLoginAt = now;
        user.LastLoginAt = now;

        var session = IssueSession(user, context, now);
        AddAuditLog(
            SecurityAuditEventTypes.LoginSuccess,
            context,
            new { provider = normalized.Provider, is_new_user = isNewUser, privileged_session = session.IsPrivileged },
            user);

        // One SaveChanges — so on a first login the user, identity, consent records, session and audit
        // row either all land or none do.
        await _db.SaveChangesAsync(ct);

        return AuthServiceResult<VerifiedOtpResult>.Ok(new VerifiedOtpResult
        {
            SessionToken = session.Token,
            ExpiresAt = session.ExpiresAt,
            Response = new VerifyOtpResponseDto
            {
                User = MapUser(user),
                IsNewUser = isNewUser
            }
        });
    }

    // ---------------------------------------------------------------------------------------------
    // Session lifecycle
    // ---------------------------------------------------------------------------------------------

    public async Task<CurrentUserResult?> GetCurrentUserAsync(string? sessionToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var tokenHash = SecretHasher.HashHighEntropy(sessionToken);

        var session = await _db.AuthSessions
            .Include(x => x.User).ThenInclude(u => u!.AuthIdentities)
            .Include(x => x.User).ThenInclude(u => u!.MfaSecret)
            .FirstOrDefaultAsync(
                x => x.SessionTokenHash == tokenHash && x.RevokedAt == null && x.ExpiresAt > now,
                ct);

        if (session?.User is null)
        {
            return null;
        }

        // Absolute lifetime. ExpiresAt slides forward on every rotation, so CreatedAt is the only
        // anchor that can ever end a continuously used session. Past that wall the session is dead:
        // revoked here, not merely rejected, so the row can never be resurrected by a later request
        // and the kill shows up in the audit trail.
        if (now >= AbsoluteExpiresAt(session))
        {
            RevokeForMaxLifetime(session, session.User, now);
            await _db.SaveChangesAsync(ct);
            return null;
        }

        if (session.User.Status != UserStatuses.Active)
        {
            // A blocked/deleted account keeps no live sessions.
            session.RevokedAt = now;
            await _db.SaveChangesAsync(ct);
            return null;
        }

        var effectiveToken = sessionToken;
        var rotated = false;

        if (ShouldRotate(session, now))
        {
            // Rotation, not re-login: the row keeps its identity, only the secret behind it changes.
            // The expiry slides so an actively used session is never cut off mid-use — but never past
            // the absolute wall, or "actively used" would simply mean "immortal".
            effectiveToken = SecretHasher.GenerateOpaqueToken();
            session.SessionTokenHash = SecretHasher.HashHighEntropy(effectiveToken);
            session.RotatedAt = now;
            session.ExpiresAt = Earliest(now.Add(SessionTtl(session.IsPrivileged)), AbsoluteExpiresAt(session));
            rotated = true;
            await _db.SaveChangesAsync(ct);
        }

        return new CurrentUserResult
        {
            User = MapUser(session.User),
            SessionToken = effectiveToken,
            Rotated = rotated,
            ExpiresAt = session.ExpiresAt,
            IsPrivilegedSession = session.IsPrivileged
        };
    }

    public async Task<AuthServiceResult<AuthMessageDto>> LogoutAsync(
        string? sessionToken,
        AuthRequestContext context,
        CancellationToken ct = default)
    {
        var done = new AuthMessageDto { Message = "Выход выполнен" };
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return AuthServiceResult<AuthMessageDto>.Ok(done);
        }

        var now = DateTimeOffset.UtcNow;
        var tokenHash = SecretHasher.HashHighEntropy(sessionToken);
        var session = await _db.AuthSessions
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.SessionTokenHash == tokenHash && x.RevokedAt == null, ct);

        if (session is not null)
        {
            session.RevokedAt = now;
            AddAuditLog(SecurityAuditEventTypes.Logout, context, new { session_id = session.Id }, session.User);
            await _db.SaveChangesAsync(ct);
        }

        // Always succeeds, so the caller can clear the cookie even for an unknown/expired token.
        return AuthServiceResult<AuthMessageDto>.Ok(done);
    }

    public async Task<AuthServiceResult<AuthMessageDto>> RevokeAllSessionsAsync(
        long userId,
        AuthRequestContext context,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null)
        {
            return AuthServiceResult<AuthMessageDto>.Fail(
                StatusCodes.Status404NotFound,
                ApiError.NotFound("Пользователь не найден."));
        }

        var sessions = await _db.AuthSessions
            .Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (var session in sessions)
        {
            session.RevokedAt = now;
        }

        AddAuditLog(SecurityAuditEventTypes.SessionRevoked, context, new { revoked_count = sessions.Count, scope = "all" }, user);
        await _db.SaveChangesAsync(ct);

        return AuthServiceResult<AuthMessageDto>.Ok(new AuthMessageDto { Message = "Все сессии завершены" });
    }

    // ---------------------------------------------------------------------------------------------
    // MFA
    // ---------------------------------------------------------------------------------------------

    public async Task<AuthServiceResult<MfaEnrollResponseDto>> EnrollMfaAsync(
        long userId,
        AuthRequestContext context,
        CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(x => x.AuthIdentities)
            .Include(x => x.MfaSecret)
            .FirstOrDefaultAsync(x => x.Id == userId, ct);

        if (user is null)
        {
            return AuthServiceResult<MfaEnrollResponseDto>.Fail(
                StatusCodes.Status404NotFound,
                ApiError.NotFound("Пользователь не найден."));
        }

        if (user.MfaSecret?.EnabledAt is not null)
        {
            // Replacing a live authenticator is an account-recovery flow, not a self-service one.
            return AuthServiceResult<MfaEnrollResponseDto>.Fail(
                StatusCodes.Status409Conflict,
                ApiError.Of("mfa_already_enabled", "Двухфакторная аутентификация уже подключена."));
        }

        var secret = _mfa.GenerateSecret();
        var recovery = _mfa.GenerateRecoveryCodes();
        var account = AccountLabel(user);

        if (user.MfaSecret is null)
        {
            user.MfaSecret = new MfaSecret { User = user };
            _db.MfaSecrets.Add(user.MfaSecret);
        }

        // A pending enrollment (EnabledAt == null) is overwritten freely — the user may have lost the
        // QR code before scanning it.
        user.MfaSecret.SecretEncrypted = _mfa.ProtectSecret(secret);
        user.MfaSecret.RecoveryCodesHash = JsonSerializer.SerializeToDocument(recovery.Hashes);
        user.MfaSecret.EnabledAt = null;

        await _db.SaveChangesAsync(ct);

        return AuthServiceResult<MfaEnrollResponseDto>.Ok(new MfaEnrollResponseDto
        {
            ProvisioningUri = _mfa.GetProvisioningUri(secret, account),
            Secret = secret,
            RecoveryCodes = recovery.Codes
        });
    }

    public async Task<AuthServiceResult<AuthMessageDto>> ConfirmMfaAsync(
        long userId,
        string? code,
        string? sessionToken,
        AuthRequestContext context,
        CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(x => x.MfaSecret)
            .FirstOrDefaultAsync(x => x.Id == userId, ct);

        if (user?.MfaSecret is null)
        {
            return AuthServiceResult<AuthMessageDto>.Fail(
                StatusCodes.Status400BadRequest,
                ApiError.Of("mfa_not_enrolled", "Сначала подключите приложение-аутентификатор."));
        }

        var secret = _mfa.UnprotectSecret(user.MfaSecret.SecretEncrypted);
        if (secret is null || !_mfa.VerifyCode(secret, code))
        {
            AddAuditLog(SecurityAuditEventTypes.LoginFailed, context, new { reason = "mfa_invalid_code" }, user);
            await _db.SaveChangesAsync(ct);

            return AuthServiceResult<AuthMessageDto>.Fail(
                StatusCodes.Status400BadRequest,
                ApiError.Of("mfa_invalid_code", "Неверный код."));
        }

        var now = DateTimeOffset.UtcNow;
        var justEnrolled = user.MfaSecret.EnabledAt is null;
        if (justEnrolled)
        {
            user.MfaSecret.EnabledAt = now;
            AddAuditLog(SecurityAuditEventTypes.MfaEnrolled, context, metadata: null, user);
        }

        // Step-up: the session that passed MFA becomes privileged for the rest of its life.
        if (!string.IsNullOrWhiteSpace(sessionToken))
        {
            var tokenHash = SecretHasher.HashHighEntropy(sessionToken);
            var session = await _db.AuthSessions
                .FirstOrDefaultAsync(x => x.SessionTokenHash == tokenHash && x.RevokedAt == null && x.ExpiresAt > now, ct);

            if (session is not null && !session.IsPrivileged)
            {
                session.IsPrivileged = true;
                // A privileged session must not outlive the short privileged TTL it just earned, nor
                // the (also shorter) privileged wall measured from when the session was created.
                var privilegedExpiry = Earliest(now.Add(SessionTtl(isPrivileged: true)), AbsoluteExpiresAt(session));
                if (session.ExpiresAt > privilegedExpiry)
                {
                    session.ExpiresAt = privilegedExpiry;
                }

                // Elevating a session already older than the privileged wall leaves nothing to elevate.
                // End it here rather than hand back a session the very next request would reject — the
                // enrollment itself still stands, the user just signs in again for a fresh one.
                if (session.ExpiresAt <= now)
                {
                    RevokeForMaxLifetime(session, user, now, context);
                }
            }
        }

        AddAuditLog(SecurityAuditEventTypes.MfaVerified, context, metadata: null, user);
        await _db.SaveChangesAsync(ct);

        return AuthServiceResult<AuthMessageDto>.Ok(new AuthMessageDto
        {
            Message = justEnrolled ? "Двухфакторная аутентификация подключена" : "Код подтверждён"
        });
    }

    // ---------------------------------------------------------------------------------------------
    // Sessions
    // ---------------------------------------------------------------------------------------------

    private (string Token, DateTimeOffset ExpiresAt, bool IsPrivileged) IssueSession(
        User user,
        AuthRequestContext context,
        DateTimeOffset now)
    {
        // 32 random bytes are already 256 bits of entropy, so the stored value is a plain SHA-256
        // digest — the raw token exists only in the cookie.
        var isPrivileged = UserRoles.Privileged.Contains(user.Role);
        var token = SecretHasher.GenerateOpaqueToken();
        var expiresAt = now.Add(SessionTtl(isPrivileged));

        _db.AuthSessions.Add(new AuthSession
        {
            User = user,
            SessionTokenHash = SecretHasher.HashHighEntropy(token),
            IpAddress = context.IpAddress,
            UserAgent = Truncate(context.UserAgent, UserAgentMaxLength),
            ExpiresAt = expiresAt,
            IsPrivileged = isPrivileged
        });

        return (token, expiresAt, isPrivileged);
    }

    private TimeSpan SessionTtl(bool isPrivileged) =>
        isPrivileged
            ? TimeSpan.FromHours(_options.PrivilegedSessionTtlHours)
            : TimeSpan.FromDays(_options.ClientSessionTtlDays);

    /// <summary>How long a session may live in total, however often it is used.</summary>
    private TimeSpan MaxSessionLifetime(bool isPrivileged)
    {
        var configured = isPrivileged
            ? TimeSpan.FromHours(_options.MaxPrivilegedSessionLifetimeHours)
            : TimeSpan.FromDays(_options.MaxSessionLifetimeDays);

        // Invariant: the wall is never closer than one sliding TTL window. A cap misconfigured to zero
        // or below the TTL can then only make sessions shorter-lived than intended — never kill a
        // brand-new session on the request right after login.
        var ttl = SessionTtl(isPrivileged);
        return configured < ttl ? ttl : configured;
    }

    /// <summary>The wall: the instant past which this session is finished no matter how recently it was
    /// used. Anchored on <see cref="AuthSession.CreatedAt"/>, which rotation never moves.</summary>
    private DateTimeOffset AbsoluteExpiresAt(AuthSession session) =>
        session.CreatedAt.Add(MaxSessionLifetime(session.IsPrivileged));

    private void RevokeForMaxLifetime(
        AuthSession session,
        User? user,
        DateTimeOffset now,
        AuthRequestContext? context = null)
    {
        session.RevokedAt = now;

        // Timestamps and ids only — a security event carries no destination, token or other PII.
        AddAuditLog(
            SecurityAuditEventTypes.SessionRevoked,
            context,
            new
            {
                session_id = session.Id,
                scope = "session",
                reason = MaxLifetimeExceededReason,
                privileged_session = session.IsPrivileged,
                session_created_at = session.CreatedAt,
                max_lifetime_hours = MaxSessionLifetime(session.IsPrivileged).TotalHours
            },
            user);
    }

    private static DateTimeOffset Earliest(DateTimeOffset left, DateTimeOffset right) =>
        left < right ? left : right;

    private bool ShouldRotate(AuthSession session, DateTimeOffset now)
    {
        var threshold = _options.SessionRotationThreshold;
        if (threshold is <= 0 or > 1)
        {
            return false;
        }

        var issuedAt = session.RotatedAt ?? session.CreatedAt;
        return now - issuedAt >= SessionTtl(session.IsPrivileged) * threshold;
    }

    // ---------------------------------------------------------------------------------------------
    // Registration side effects
    // ---------------------------------------------------------------------------------------------

    /// <summary>Records the two things a new account agrees to: processing of personal data (152-FZ),
    /// logged in <see cref="ConsentLog"/>, and the client offer, logged as a
    /// <see cref="LegalAcceptance"/> against the versioned document — the split the entity model
    /// prescribes. Consent is by conclusive action: the login form states both above the submit button.</summary>
    private void AddRegistrationConsent(User user, AuthIdentity identity, AuthRequestContext context, long termsDocumentId)
    {
        var ip = context.IpAddress ?? System.Net.IPAddress.None;
        var userAgent = Truncate(context.UserAgent, UserAgentMaxLength);
        var now = DateTimeOffset.UtcNow;

        _db.ConsentLogs.Add(new ConsentLog
        {
            User = user,
            Email = identity.Email,
            Phone = identity.Phone,
            ConsentType = ConsentTypes.PersonalData,
            DocumentVersion = PlaceholderDocumentVersion,
            IpAddress = ip,
            UserAgent = userAgent,
            AcceptedAt = now
        });

        _db.LegalAcceptances.Add(new LegalAcceptance
        {
            User = user,
            DocumentId = termsDocumentId,
            IpAddress = ip,
            UserAgent = userAgent,
            AcceptedAt = now
        });
    }

    /// <summary>Returns the id of the placeholder document of <paramref name="type"/>, creating it on
    /// first use so registration always has something to attach an acceptance to. Real content and
    /// versioning land in the Legal task.</summary>
    private async Task<long> EnsurePlaceholderLegalDocumentAsync(string type, CancellationToken ct)
    {
        var existing = await _db.LegalDocuments
            .Where(x => x.Type == type && x.Version == PlaceholderDocumentVersion && x.Locale == "ru")
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            return existing.Value;
        }

        var document = new LegalDocument
        {
            Type = type,
            Version = PlaceholderDocumentVersion,
            Locale = "ru",
            ContentHash = SecretHasher.HashHighEntropy($"{type}:{PlaceholderDocumentVersion}"),
            EffectiveAt = DateTimeOffset.UtcNow,
            Status = LegalDocumentStatuses.Draft
        };

        _db.LegalDocuments.Add(document);

        try
        {
            await _db.SaveChangesAsync(ct);
            return document.Id;
        }
        catch (DbUpdateException)
        {
            // Two first-ever registrations raced; the unique index kept exactly one. Drop ours and use
            // the winner's row.
            _db.Entry(document).State = EntityState.Detached;

            var winner = await _db.LegalDocuments
                .Where(x => x.Type == type && x.Version == PlaceholderDocumentVersion && x.Locale == "ru")
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync(ct);

            if (winner is null)
            {
                throw;
            }

            return winner.Value;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Lookups, mapping, helpers
    // ---------------------------------------------------------------------------------------------

    private Task<AuthIdentity?> FindIdentityAsync(NormalizedRequest normalized, CancellationToken ct)
    {
        var query = _db.AuthIdentities
            .Include(x => x.User).ThenInclude(u => u!.AuthIdentities)
            .Include(x => x.User).ThenInclude(u => u!.MfaSecret)
            .Where(x => x.Provider == normalized.Provider);

        query = normalized.Channel == OtpChannels.Email
            ? query.Where(x => x.Email == normalized.Destination)
            : query.Where(x => x.Phone == normalized.Destination);

        return query.FirstOrDefaultAsync(ct);
    }

    private static AuthUserDto MapUser(User user)
    {
        // Prefer a verified contact — an account can accumulate several identities over time.
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

        return new AuthUserDto
        {
            Id = user.Id,
            Role = user.Role,
            Status = user.Status,
            DisplayName = user.DisplayName,
            Email = email,
            Phone = phone,
            MfaEnabled = user.MfaSecret?.EnabledAt is not null
        };
    }

    private static string AccountLabel(User user)
    {
        var identity = user.AuthIdentities.FirstOrDefault(x => x.Email != null)
                       ?? user.AuthIdentities.FirstOrDefault(x => x.Phone != null);
        return identity?.Email ?? identity?.Phone ?? $"user-{user.Id}";
    }

    /// <summary><paramref name="context"/> is nullable because not every audited event happens on a
    /// request that carries one: <see cref="GetCurrentUserAsync"/> is reached through
    /// <see cref="IAuthService"/>, which takes only the token, so a session revoked there is recorded
    /// without an IP/user-agent rather than with a fabricated one.</summary>
    private void AddAuditLog(string eventType, AuthRequestContext? context, object? metadata, User? user = null)
    {
        _db.SecurityAuditLogs.Add(new SecurityAuditLog
        {
            User = user,
            EventType = eventType,
            ActorRole = user?.Role,
            IpAddress = context?.IpAddress,
            UserAgent = Truncate(context?.UserAgent, UserAgentMaxLength),
            Metadata = metadata is null ? null : JsonSerializer.SerializeToDocument(metadata)
        });
    }

    private async Task<bool> TrySendCodeAsync(string destination, string code, CancellationToken ct)
    {
        try
        {
            await _emailSender.SendAsync(
                new EmailMessage
                {
                    To = destination,
                    Subject = "Код для входа — Память рядом",
                    HtmlBody = BuildCodeHtml(code),
                    TextBody = BuildCodeText(code)
                },
                ct);
            return true;
        }
        catch (Exception ex)
        {
            // Never log the code or the raw destination — only a stable pseudonymous key.
            _logger.LogError(ex, "Failed to deliver a login code. DestinationKey={DestinationKey}", DestinationKey(destination));
            return false;
        }
    }

    private string BuildCodeText(string code) =>
        $"Ваш код для входа: {code}\n\n" +
        $"Код действует {_options.CodeTtlMinutes} минут. " +
        "Если вы не запрашивали вход, просто проигнорируйте это письмо.";

    private string BuildCodeHtml(string code) =>
        $"""
        <div style="font-family:Arial,Helvetica,sans-serif;max-width:480px;margin:0 auto;color:#111827">
          <h2 style="margin:0 0 16px;font-size:20px">Вход в «Память рядом»</h2>
          <p style="margin:0 0 12px;font-size:15px">Ваш код для входа:</p>
          <p style="font-size:32px;font-weight:700;letter-spacing:6px;margin:0 0 16px">{code}</p>
          <p style="color:#6b7280;font-size:13px;margin:0">
            Код действует {_options.CodeTtlMinutes} минут. Если вы не запрашивали вход, проигнорируйте это письмо.
          </p>
        </div>
        """;

    private sealed record NormalizedRequest(string Destination, string Channel, string Purpose, string Provider);

    private static NormalizedRequest NormalizeRequest(
        string? destination,
        string? channel,
        string? purpose,
        out List<ApiError> errors)
    {
        errors = new List<ApiError>();

        var normalizedChannel = Clean(channel)?.ToLowerInvariant() ?? OtpChannels.Email;
        if (!OtpChannels.All.Contains(normalizedChannel))
        {
            errors.Add(ApiError.Validation("Недопустимый канал доставки.", new { field = "channel" }));
            normalizedChannel = OtpChannels.Email;
        }

        var normalizedPurpose = Clean(purpose)?.ToLowerInvariant() ?? OtpPurposes.Login;
        if (!OtpPurposes.All.Contains(normalizedPurpose))
        {
            errors.Add(ApiError.Validation("Недопустимое назначение кода.", new { field = "purpose" }));
            normalizedPurpose = OtpPurposes.Login;
        }

        string normalizedDestination;
        if (normalizedChannel == OtpChannels.Email)
        {
            normalizedDestination = NormalizeEmail(destination) ?? string.Empty;
            if (normalizedDestination.Length is 0 or > 320 || !IsValidEmail(normalizedDestination))
            {
                errors.Add(ApiError.Validation("Укажите корректный email.", new { field = "destination" }));
            }
        }
        else
        {
            normalizedDestination = NormalizePhone(destination) ?? string.Empty;
            if (normalizedDestination.Length is < 11 or > 16)
            {
                errors.Add(ApiError.Validation("Укажите корректный номер телефона.", new { field = "destination" }));
            }
        }

        var provider = normalizedChannel == OtpChannels.Email ? AuthProviders.Email : AuthProviders.Phone;
        return new NormalizedRequest(normalizedDestination, normalizedChannel, normalizedPurpose, provider);
    }

    /// <summary>The code is hashed together with the destination and purpose, so a hash captured for
    /// one flow can't be replayed against another.</summary>
    private static string BuildCodePayload(string destination, string purpose, string code) =>
        $"{destination}:{purpose}:{code}";

    private static string GenerateNumericCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    /// <summary>A stable, non-reversible key for correlating log lines about one destination without
    /// putting the email/phone itself in the log.</summary>
    private static string DestinationKey(string destination) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(destination)))[..12].ToLowerInvariant();

    private static string? NormalizeEmail(string? email)
    {
        var trimmed = Clean(email)?.ToLowerInvariant();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    /// <summary>Reduces a Russian number to E.164-ish digits: <c>8 (912) 345-67-89</c> -> <c>+79123456789</c>.</summary>
    private static string? NormalizePhone(string? phone)
    {
        var cleaned = Clean(phone);
        if (cleaned is null)
        {
            return null;
        }

        var digits = new string(cleaned.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 0)
        {
            return null;
        }

        if (digits.Length == 11 && digits[0] == '8')
        {
            digits = string.Concat("7", digits.AsSpan(1));
        }

        return $"+{digits}";
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];

    private static bool IsValidEmail(string email)
    {
        try
        {
            var parsed = new MailAddress(email);
            return string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            return false;
        }
    }
}

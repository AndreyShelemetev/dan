using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PamyatRyadom.Api.Data;
using PamyatRyadom.Api.Dtos.Media;
using PamyatRyadom.Api.Models.BurialSites;
using PamyatRyadom.Api.Models.Auth;
using PamyatRyadom.Api.Models.Dispatch;
using PamyatRyadom.Api.Models.Media;
using PamyatRyadom.Api.Models.Orders;
using PamyatRyadom.Api.Services.BurialSites;
using PamyatRyadom.Api.Services.Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace PamyatRyadom.Api.Services.Media;

public interface IMediaService
{
    Task<ServiceResult<UploadSessionDto>> CreateUploadSessionAsync(
        long userId, CreateUploadSessionDto request, CancellationToken ct = default);

    Task<ServiceResult<MediaAssetDto>> CompleteUploadAsync(long userId, long assetId, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<MediaAssetDto>>> ListAsync(
        long userId, string ownerType, long ownerId, CancellationToken ct = default);

    Task<ServiceResult<object>> DeleteAsync(long userId, long assetId, CancellationToken ct = default);
}

public sealed class MediaService : IMediaService
{
    private const int MaxDimension = 2560;
    private const int ThumbnailSize = 512;

    private readonly AppDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly IBurialSiteService _burialSites;
    private readonly StorageOptions _options;
    private readonly ILogger<MediaService> _logger;

    public MediaService(
        AppDbContext db,
        IObjectStorage storage,
        IBurialSiteService burialSites,
        IOptions<StorageOptions> options,
        ILogger<MediaService> logger)
    {
        _db = db;
        _storage = storage;
        _burialSites = burialSites;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<UploadSessionDto>> CreateUploadSessionAsync(
        long userId, CreateUploadSessionDto request, CancellationToken ct = default)
    {
        if (!MediaOwnerTypes.All.Contains(request.OwnerType))
        {
            return ServiceResult<UploadSessionDto>.Validation("Неизвестный тип объекта.", new { field = "ownerType" });
        }

        if (!MediaPhases.All.Contains(request.Phase))
        {
            return ServiceResult<UploadSessionDto>.Validation("Неизвестный этап съёмки.", new { field = "phase" });
        }

        if (!ImageSignature.AllowedUploadTypes.Contains(request.ContentType))
        {
            return ServiceResult<UploadSessionDto>.Validation(
                "Поддерживаются фотографии JPEG, PNG, WebP и HEIC.", new { field = "contentType" });
        }

        if (request.SizeBytes <= 0 || request.SizeBytes > _options.MaxUploadBytes)
        {
            var limitMb = _options.MaxUploadBytes / (1024 * 1024);
            return ServiceResult<UploadSessionDto>.Validation(
                $"Файл слишком большой. Максимум — {limitMb} МБ.", new { field = "sizeBytes" });
        }

        var allowed = await CanWriteOwnerAsync(userId, request.OwnerType, request.OwnerId, ct);
        if (!allowed)
        {
            // Same reasoning as burial sites: 404 rather than 403, so the response cannot be
            // used to discover which records exist.
            return ServiceResult<UploadSessionDto>.NotFound();
        }

        // Random key, never derived from the file name or the owning record. A guessable key
        // would be a second door into the object that skips the ownership check entirely.
        var key = $"{request.OwnerType}/{request.OwnerId}/{Guid.NewGuid():N}";

        var asset = new MediaAsset
        {
            OwnerType = request.OwnerType,
            OwnerId = request.OwnerId,
            Phase = request.Phase,
            StorageKey = key,
            ContentType = request.ContentType,
            FileSizeBytes = request.SizeBytes,
            Status = MediaStatuses.Uploading,
            UploadedByUserId = userId,
        };

        _db.MediaAssets.Add(asset);
        await _db.SaveChangesAsync(ct);

        var uploadUrl = _storage.CreateUploadUrl(key, request.ContentType);

        _logger.LogInformation(
            "Upload session {AssetId} opened for {OwnerType}/{OwnerId} by user {UserId}",
            asset.Id, request.OwnerType, request.OwnerId, userId);

        return ServiceResult<UploadSessionDto>.Created(new UploadSessionDto
        {
            AssetId = asset.Id,
            UploadUrl = uploadUrl,
            ContentType = request.ContentType,
            ExpiresInSeconds = _options.UploadUrlTtlMinutes * 60,
        });
    }

    /// <summary>
    /// Called once the browser has PUT the bytes. This is where the file is actually checked,
    /// normalised and made servable.
    ///
    /// Runs inline rather than in a background worker: for a phone photo the whole pass is well
    /// under a second, and an inline pass means the client learns immediately that its file was
    /// rejected. It is written as one self-contained step so moving it onto a queue later — which
    /// video, or an antivirus round trip, would demand — does not require reshaping the caller.
    /// </summary>
    public async Task<ServiceResult<MediaAssetDto>> CompleteUploadAsync(
        long userId, long assetId, CancellationToken ct = default)
    {
        var asset = await _db.MediaAssets.FirstOrDefaultAsync(a => a.Id == assetId, ct);
        if (asset is null || asset.UploadedByUserId != userId)
        {
            return ServiceResult<MediaAssetDto>.NotFound();
        }

        if (asset.Status == MediaStatuses.Ready)
        {
            // Idempotent: a retried or double-fired completion must not reprocess or duplicate.
            return ServiceResult<MediaAssetDto>.Ok(await MapAsync(asset, ct));
        }

        if (asset.Status != MediaStatuses.Uploading)
        {
            return ServiceResult<MediaAssetDto>.Validation("Файл уже обработан или отклонён.");
        }

        var head = await _storage.HeadAsync(asset.StorageKey, ct);
        if (head is null)
        {
            return ServiceResult<MediaAssetDto>.Validation("Файл не загружен.");
        }

        if (head.ContentLength > _options.MaxUploadBytes)
        {
            await QuarantineAsync(asset, "Размер превышает лимит.", ct);
            return ServiceResult<MediaAssetDto>.Validation("Файл слишком большой.");
        }

        asset.Status = MediaStatuses.Scanning;
        await _db.SaveChangesAsync(ct);

        try
        {
            await using var raw = await _storage.OpenReadAsync(asset.StorageKey, ct);

            var header = new byte[16];
            var read = await raw.ReadAsync(header.AsMemory(), ct);
            raw.Position = 0;

            var detected = ImageSignature.Detect(header.AsSpan(0, read));
            if (detected is null)
            {
                await QuarantineAsync(asset, "Содержимое не является изображением.", ct);
                return ServiceResult<MediaAssetDto>.Validation(
                    "Файл не похож на фотографию. Загрузите JPEG, PNG, WebP или HEIC.");
            }

            asset.ChecksumSha256 = Convert.ToHexString(await SHA256.HashDataAsync(raw, ct)).ToLowerInvariant();
            raw.Position = 0;

            // Re-encoding to WebP is not only about size: decoding and re-writing the pixels
            // discards EXIF wholesale, and grave photos carry GPS coordinates that must not
            // travel with a file the client may later download and share.
            using var image = await Image.LoadAsync(raw, ct);

            if (image.Width > MaxDimension || image.Height > MaxDimension)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(MaxDimension, MaxDimension),
                }));
            }

            // Read after the resize: these describe the file that is actually stored, so a client
            // laying out the gallery gets the real pixel size rather than the camera's.
            var width = image.Width;
            var height = image.Height;

            var encoder = new WebpEncoder { Quality = 82 };

            using var full = new MemoryStream();
            await image.SaveAsync(full, encoder, ct);
            full.Position = 0;

            // Captured before the upload: whatever the storage client does with the stream,
            // the size we record is the size we produced.
            var readyBytes = full.Length;

            var readyKey = $"{asset.StorageKey}.webp";
            await _storage.PutAsync(readyKey, full, ImageSignature.WebP, ct);

            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(ThumbnailSize, ThumbnailSize),
            }));

            using var thumb = new MemoryStream();
            await image.SaveAsync(thumb, encoder, ct);
            thumb.Position = 0;

            var thumbKey = $"{asset.StorageKey}.thumb.webp";
            await _storage.PutAsync(thumbKey, thumb, ImageSignature.WebP, ct);

            // The original is dropped once the sanitised copy exists — it is the only copy that
            // still carries EXIF, and keeping it would quietly defeat the point of re-encoding.
            await _storage.DeleteAsync(asset.StorageKey, ct);

            asset.StorageKey = readyKey;
            asset.ThumbnailKey = thumbKey;
            asset.ContentType = ImageSignature.WebP;
            asset.FileSizeBytes = readyBytes;
            asset.Width = width;
            asset.Height = height;
            asset.Status = MediaStatuses.Ready;
            asset.ReadyAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Media asset {AssetId} is ready", asset.Id);
            return ServiceResult<MediaAssetDto>.Ok(await MapAsync(asset, ct));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // No filename, no key, no owner detail in the message — a corrupt upload should not
            // put user data into the log.
            _logger.LogWarning(exception, "Processing failed for media asset {AssetId}", asset.Id);
            await QuarantineAsync(asset, "Не удалось обработать изображение.", ct);
            return ServiceResult<MediaAssetDto>.Validation("Не удалось обработать фотографию. Попробуйте другой файл.");
        }
    }

    public async Task<ServiceResult<IReadOnlyList<MediaAssetDto>>> ListAsync(
        long userId, string ownerType, long ownerId, CancellationToken ct = default)
    {
        if (!await CanReadOwnerAsync(userId, ownerType, ownerId, ct))
        {
            return ServiceResult<IReadOnlyList<MediaAssetDto>>.NotFound();
        }

        var assets = await _db.MediaAssets
            .AsNoTracking()
            .Where(a => a.OwnerType == ownerType && a.OwnerId == ownerId && a.Status == MediaStatuses.Ready)
            .OrderBy(a => a.Id)
            .ToListAsync(ct);

        var dtos = new List<MediaAssetDto>(assets.Count);
        foreach (var asset in assets)
        {
            dtos.Add(await MapAsync(asset, ct));
        }

        return ServiceResult<IReadOnlyList<MediaAssetDto>>.Ok(dtos);
    }

    public async Task<ServiceResult<object>> DeleteAsync(long userId, long assetId, CancellationToken ct = default)
    {
        var asset = await _db.MediaAssets.FirstOrDefaultAsync(a => a.Id == assetId, ct);
        if (asset is null || asset.Status == MediaStatuses.Deleted)
        {
            return ServiceResult<object>.NotFound();
        }

        if (!await CanWriteOwnerAsync(userId, asset.OwnerType, asset.OwnerId, ct))
        {
            return ServiceResult<object>.NotFound();
        }

        // Tombstone first, remove the bytes after: if the delete call fails the record is already
        // unservable, which is the safer order for something that leaks when it lingers.
        asset.Status = MediaStatuses.Deleted;
        asset.ReadyAt = null;
        await _db.SaveChangesAsync(ct);

        foreach (var key in new[] { asset.StorageKey, asset.ThumbnailKey })
        {
            if (string.IsNullOrEmpty(key)) continue;
            try
            {
                await _storage.DeleteAsync(key, ct);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not delete stored object for asset {AssetId}", asset.Id);
            }
        }

        return ServiceResult<object>.Ok(new { deleted = true });
    }

    private async Task<MediaAssetDto> MapAsync(MediaAsset asset, CancellationToken ct)
    {
        await Task.CompletedTask;

        return new MediaAssetDto
        {
            Id = asset.Id,
            Phase = asset.Phase,
            Status = asset.Status,
            Width = asset.Width,
            Height = asset.Height,
            // Signed per response, never stored: the link has to expire, and a cached one would not.
            Url = asset.Status == MediaStatuses.Ready ? _storage.CreateDownloadUrl(asset.StorageKey) : null,
            ThumbnailUrl = asset.Status == MediaStatuses.Ready && asset.ThumbnailKey is not null
                ? _storage.CreateDownloadUrl(asset.ThumbnailKey)
                : null,
            CreatedAt = asset.CreatedAt,
        };
    }

    private Task<bool> CanReadOwnerAsync(long userId, string ownerType, long ownerId, CancellationToken ct) =>
        ownerType switch
        {
            MediaOwnerTypes.BurialSite => HasBurialSiteAccessAsync(userId, ownerId, manageOnly: false, ct),
            MediaOwnerTypes.Order => OwnsOrderAsync(userId, ownerId, forWriting: false, ct),
            MediaOwnerTypes.Visit => CanSeeVisitAsync(userId, ownerId, forWriting: false, ct),
            // Disputes get their own rule when that module lands. Denying by default means a new
            // owner type cannot accidentally inherit open access.
            _ => Task.FromResult(false),
        };

    private Task<bool> CanWriteOwnerAsync(long userId, string ownerType, long ownerId, CancellationToken ct) =>
        ownerType switch
        {
            MediaOwnerTypes.BurialSite => HasBurialSiteAccessAsync(userId, ownerId, manageOnly: true, ct),
            MediaOwnerTypes.Order => OwnsOrderAsync(userId, ownerId, forWriting: true, ct),
            MediaOwnerTypes.Visit => CanSeeVisitAsync(userId, ownerId, forWriting: true, ct),
            _ => Task.FromResult(false),
        };

    /// <summary>Staff who need to look at what a client attached in order to do their job:
    /// price the request, check the work against it, answer a question about it. Finance and
    /// executors are absent on purpose — neither prices nor reviews a request from these photos,
    /// and an executor sees the site through their own visit instead.</summary>
    private static readonly string[] OrderPhotoViewerRoles =
    {
        UserRoles.Dispatcher, UserRoles.Qa, UserRoles.Support, UserRoles.Admin, UserRoles.Superadmin
    };

    /// <summary>
    /// Photos the client attaches to their own request.
    ///
    /// Readable for as long as they own the order; writable only while the order is still theirs
    /// to shape. Once it has been priced, the photos are part of what the estimate was based on —
    /// letting the client swap them afterwards would quietly change the evidence behind an
    /// agreed price.
    ///
    /// Operational staff can read but never write: a dispatcher has to see the grave to quote a
    /// job, but the client's own evidence is not something we edit on their behalf.
    /// </summary>
    private async Task<bool> OwnsOrderAsync(long userId, long orderId, bool forWriting, CancellationToken ct)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new { o.CustomerUserId, o.Status })
            .FirstOrDefaultAsync(ct);

        if (order is null)
        {
            return false;
        }

        if (order.CustomerUserId != userId)
        {
            return !forWriting && await IsOrderPhotoViewerAsync(userId, ct);
        }

        return !forWriting || OrderStateMachine.IsClientEditable(order.Status);
    }

    private async Task<bool> IsOrderPhotoViewerAsync(long userId, CancellationToken ct)
    {
        var role = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && u.Status == UserStatuses.Active)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(ct);

        return role is not null && OrderPhotoViewerRoles.Contains(role);
    }

    /// <summary>
    /// The photographs that make up a visit report.
    ///
    /// Written only by the executor doing the job, and only while the visit is still theirs to
    /// work on — a report editable after it is filed is not evidence of anything.
    ///
    /// Read by that executor, by staff who review the work, and by the client who paid for it —
    /// but the client only once QA has approved it. Showing an unreviewed report early turns a
    /// problem we would have caught into a dispute the client raises (BR-010).
    /// </summary>
    private async Task<bool> CanSeeVisitAsync(long userId, long visitId, bool forWriting, CancellationToken ct)
    {
        var visit = await _db.Visits
            .AsNoTracking()
            .Where(v => v.Id == visitId)
            .Select(v => new { v.ExecutorUserId, v.Status, v.OrderId })
            .FirstOrDefaultAsync(ct);

        if (visit is null)
        {
            return false;
        }

        if (visit.ExecutorUserId == userId)
        {
            return !forWriting || VisitStatuses.IsExecutorActionable(visit.Status);
        }

        if (forWriting)
        {
            // Nobody else writes a report. Not QA, not an admin: a report someone other than the
            // executor can add photographs to is no longer a record of what they found.
            return false;
        }

        if (await IsOrderPhotoViewerAsync(userId, ct))
        {
            return true;
        }

        // The client who paid for it, once it has passed QA.
        return visit.Status == VisitStatuses.Approved &&
               await _db.Orders.AnyAsync(o => o.Id == visit.OrderId && o.CustomerUserId == userId, ct);
    }

    private async Task<bool> HasBurialSiteAccessAsync(long userId, long siteId, bool manageOnly, CancellationToken ct)
    {
        var permission = await _burialSites.ResolvePermissionAsync(userId, siteId, ct);
        if (permission is null)
        {
            return false;
        }

        // Viewing is open to anyone with access; adding or removing photos is an edit, so it
        // needs the same right as editing the record itself.
        return !manageOnly || permission == BurialSitePermissions.Manage;
    }

    private async Task QuarantineAsync(MediaAsset asset, string reason, CancellationToken ct)
    {
        asset.Status = MediaStatuses.Quarantined;
        asset.ModerationNote = reason;
        asset.ReadyAt = null;
        await _db.SaveChangesAsync(ct);

        // The original plus anything already derived from it. Processing can fail after a
        // derivative has been written, and a half-finished set left in the bucket is storage
        // nobody owns and nothing will ever clean up.
        var keys = new[]
        {
            asset.StorageKey,
            $"{asset.StorageKey}.webp",
            $"{asset.StorageKey}.thumb.webp",
            asset.ThumbnailKey,
        };

        foreach (var key in keys.Where(k => !string.IsNullOrEmpty(k)).Distinct())
        {
            try
            {
                await _storage.DeleteAsync(key!, ct);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not remove quarantined object for asset {AssetId}", asset.Id);
            }
        }
    }
}

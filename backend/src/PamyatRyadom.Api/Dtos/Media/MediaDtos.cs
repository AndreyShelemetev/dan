using System.ComponentModel.DataAnnotations;

namespace PamyatRyadom.Api.Dtos.Media;

public sealed class CreateUploadSessionDto
{
    /// <summary>"burial_site" today; orders, visits and disputes follow with their modules.</summary>
    [Required]
    public string OwnerType { get; init; } = string.Empty;

    [Required]
    public long OwnerId { get; init; }

    /// <summary>"reference" for client-supplied context photos; the before/process/after phases
    /// belong to executor visits.</summary>
    public string Phase { get; init; } = "reference";

    [Required]
    public string ContentType { get; init; } = string.Empty;

    /// <summary>Declared up front so an oversized file is refused before a single byte is
    /// uploaded, rather than after.</summary>
    [Required]
    public long SizeBytes { get; init; }
}

public sealed class UploadSessionDto
{
    public long AssetId { get; init; }

    /// <summary>Presigned PUT target. The browser uploads straight to storage — the bytes never
    /// pass through the API.</summary>
    public string UploadUrl { get; init; } = string.Empty;

    /// <summary>Must be sent as the Content-Type of the PUT: it is part of the signature.</summary>
    public string ContentType { get; init; } = string.Empty;

    public int ExpiresInSeconds { get; init; }
}

public sealed class MediaAssetDto
{
    public long Id { get; init; }
    public string Phase { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int? Width { get; init; }
    public int? Height { get; init; }

    /// <summary>Short-lived signed link, minted for this response. There is no permanent URL for
    /// any asset — see BR-015.</summary>
    public string? Url { get; init; }
    public string? ThumbnailUrl { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

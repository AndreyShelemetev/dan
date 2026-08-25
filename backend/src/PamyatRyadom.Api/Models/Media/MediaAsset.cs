namespace PamyatRyadom.Api.Models.Media;

/// <summary>
/// Metadata for one stored file. The bytes live in a private S3-compatible bucket; the
/// database only ever holds the key, never the content.
///
/// Photographs are the core evidence of this product — the whole trust proposition rests on
/// them — so an asset is only ever served through a short-lived presigned URL issued after an
/// ownership check. There is no public URL for any asset, by design.
/// </summary>
public sealed class MediaAsset
{
    public long Id { get; set; }

    /// <summary>Polymorphic owner. Kept as (type, id) rather than a nullable FK per owner
    /// kind so that adding a new owner type later does not mean another migration adding
    /// another mostly-null column.</summary>
    public string OwnerType { get; set; } = string.Empty;
    public long OwnerId { get; set; }

    public string Phase { get; set; } = MediaPhases.Reference;

    /// <summary>Key in the private bucket. Randomised, never derived from user input —
    /// a guessable key would be a second way in that bypasses the ownership check.</summary>
    public string StorageKey { get; set; } = string.Empty;
    public string? ThumbnailKey { get; set; }

    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }

    /// <summary>Set once the worker has read the object back; also used to spot duplicate
    /// uploads of the same file.</summary>
    public string? ChecksumSha256 { get; set; }

    public int? Width { get; set; }
    public int? Height { get; set; }

    public string Status { get; set; } = MediaStatuses.Uploading;

    /// <summary>Who initiated the upload. Retained for the audit trail even after the
    /// uploader loses access to the owning object.</summary>
    public long? UploadedByUserId { get; set; }

    public DateTimeOffset? ReadyAt { get; set; }

    /// <summary>Why the file was quarantined, when it was. Free text from the scan pipeline.</summary>
    public string? ModerationNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public static class MediaOwnerTypes
{
    public const string BurialSite = "burial_site";
    public const string Order = "order";
    public const string Visit = "visit";
    public const string Dispute = "dispute";

    public static readonly IReadOnlyCollection<string> All = new[] { BurialSite, Order, Visit, Dispute };
}

public static class MediaPhases
{
    /// <summary>Client-supplied context: what the place looks like, an old photo, a sketch.</summary>
    public const string Reference = "reference";

    /// <summary>Mandatory "before" angles, shot on site prior to any work.</summary>
    public const string Before = "before";

    /// <summary>Work in progress and materials used.</summary>
    public const string Process = "process";

    /// <summary>Mandatory "after" angles, repeating the "before" framing so the two can be
    /// compared side by side during QA.</summary>
    public const string After = "after";

    /// <summary>Receipts, permits, generated report PDFs.</summary>
    public const string Document = "document";

    public static readonly IReadOnlyCollection<string> All = new[] { Reference, Before, Process, After, Document };
}

public static class MediaStatuses
{
    /// <summary>A presigned upload has been issued but completion was never confirmed.</summary>
    public const string Uploading = "uploading";

    /// <summary>Bytes are in the bucket; signature, antivirus and transcoding are pending.</summary>
    public const string Scanning = "scanning";

    /// <summary>Passed every check. This is the only status that may ever be served.</summary>
    public const string Ready = "ready";

    /// <summary>Failed a check. Withheld from everyone except security staff.</summary>
    public const string Quarantined = "quarantined";

    /// <summary>Tombstoned; the object is removed from the bucket asynchronously.</summary>
    public const string Deleted = "deleted";

    public static readonly IReadOnlyCollection<string> All = new[] { Uploading, Scanning, Ready, Quarantined, Deleted };
}

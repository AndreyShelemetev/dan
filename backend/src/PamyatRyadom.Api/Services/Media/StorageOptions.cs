namespace PamyatRyadom.Api.Services.Media;

/// <summary>
/// Private S3-compatible bucket holding every photo in the product.
///
/// Bound from the <c>Storage</c> configuration section (S3__* environment variables in
/// docker-compose). In development this points at the MinIO container; in production it must
/// point at a bucket in a Russian region — client photos and grave coordinates are personal
/// data subject to the localisation rule.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Address the API itself talks to (inside the compose network in dev).</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Address presigned URLs are signed for — the one a browser can actually reach.
    ///
    /// These differ in development: the API reaches MinIO at <c>http://minio:9000</c>, which means
    /// nothing outside the compose network, while the browser must use the published port. The
    /// host is part of an AWS Signature V4 signature, so a link signed for one host and requested
    /// on another is rejected — rewriting the URL afterwards is not an option. Left empty, it
    /// falls back to <see cref="Endpoint"/>, which is the right behaviour in production where a
    /// real bucket has one address for everybody.</summary>
    public string PublicEndpoint { get; set; } = string.Empty;

    public string Bucket { get; set; } = "pamyat-media";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Region { get; set; } = "ru-central1";

    /// <summary>MinIO and most self-hosted gateways need path-style addressing
    /// (<c>host/bucket/key</c>); AWS itself prefers virtual-host style.</summary>
    public bool UsePathStyle { get; set; } = true;

    /// <summary>Lifetime of an upload link. Short: it is handed to a browser and grants write
    /// access to one key.</summary>
    public int UploadUrlTtlMinutes { get; set; } = 10;

    /// <summary>Lifetime of a view link. Deliberately brief — a leaked link is a leaked photo,
    /// and this is the one control standing between a private grave photo and the open web.</summary>
    public int DownloadUrlTtlMinutes { get; set; } = 5;

    /// <summary>Hard ceiling on an accepted upload. Phone photos rarely exceed this, and a
    /// larger value mostly widens the denial-of-service surface.</summary>
    public long MaxUploadBytes { get; set; } = 20L * 1024 * 1024;
}

using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace PamyatRyadom.Api.Services.Media;

public interface IObjectStorage
{
    /// <summary>A short-lived URL the browser may PUT one object to. Content type and length are
    /// baked into the signature, so a client cannot swap a 20 MB JPEG for a 2 GB archive after
    /// the server has approved the request.</summary>
    string CreateUploadUrl(string key, string contentType);

    /// <summary>A short-lived URL for reading one object. Issued only after the caller's right to
    /// the owning record has been checked.</summary>
    string CreateDownloadUrl(string key);

    Task<Stream> OpenReadAsync(string key, CancellationToken ct = default);
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default);
    Task<ObjectHead?> HeadAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>Creates the bucket when it is missing. Only used in development against MinIO —
    /// a production bucket is provisioned deliberately, with its own access policy and
    /// lifecycle rules, not by an application on startup.</summary>
    Task EnsureBucketAsync(CancellationToken ct = default);
}

public sealed record ObjectHead(long ContentLength, string? ContentType);

public sealed class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly IAmazonS3 _s3;

    /// <summary>Second client, configured against the browser-reachable address, used only to
    /// sign URLs. It issues no requests of its own — see StorageOptions.PublicEndpoint.</summary>
    private readonly IAmazonS3 _signer;

    private readonly StorageOptions _options;
    private readonly ILogger<S3ObjectStorage> _logger;

    public S3ObjectStorage(IOptions<StorageOptions> options, ILogger<S3ObjectStorage> logger)
    {
        _options = options.Value;
        _logger = logger;

        var config = new AmazonS3Config
        {
            ServiceURL = _options.Endpoint,
            ForcePathStyle = _options.UsePathStyle,
            AuthenticationRegion = _options.Region,
            // The SDK upgrades to https unless told otherwise, even when ServiceURL says http —
            // which makes a presigned link for a plain-http MinIO unusable in a browser.
            UseHttp = IsPlainHttp(_options.Endpoint),
        };

        _s3 = new AmazonS3Client(_options.AccessKey, _options.SecretKey, config);

        var publicEndpoint = string.IsNullOrWhiteSpace(_options.PublicEndpoint)
            ? _options.Endpoint
            : _options.PublicEndpoint;

        _signer = publicEndpoint == _options.Endpoint
            ? _s3
            : new AmazonS3Client(_options.AccessKey, _options.SecretKey, new AmazonS3Config
            {
                ServiceURL = publicEndpoint,
                ForcePathStyle = _options.UsePathStyle,
                AuthenticationRegion = _options.Region,
                UseHttp = IsPlainHttp(publicEndpoint),
            });
    }

    private static bool IsPlainHttp(string endpoint) =>
        endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Forces the signed link back onto the scheme the endpoint was configured with.
    ///
    /// This SDK version resolves S3 endpoints through the rules provider, which hands back an
    /// https URL even when ServiceURL is plain http and UseHttp is set — and a browser then fails
    /// the upload with ERR_SSL_PROTOCOL_ERROR against a MinIO that speaks no TLS.
    ///
    /// Rewriting the scheme is safe, and specifically not a way of tampering with the signature:
    /// AWS Signature V4 covers the method, path, query and the host header, but never the scheme.
    /// Host and path are left exactly as signed. In production, where the endpoint is https, this
    /// is a no-op.
    /// </summary>
    private string MatchEndpointScheme(string url)
    {
        var endpoint = string.IsNullOrWhiteSpace(_options.PublicEndpoint)
            ? _options.Endpoint
            : _options.PublicEndpoint;

        if (!IsPlainHttp(endpoint) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        return string.Concat("http://", url.AsSpan("https://".Length));
    }

    public string CreateUploadUrl(string key, string contentType) =>
        MatchEndpointScheme(_signer.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.AddMinutes(_options.UploadUrlTtlMinutes),
            ContentType = contentType,
        }));

    public string CreateDownloadUrl(string key) =>
        MatchEndpointScheme(_signer.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(_options.DownloadUrlTtlMinutes),
        }));

    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var response = await _s3.GetObjectAsync(_options.Bucket, key, ct);

        // Copied into memory so the S3 response can be disposed here rather than leaking its
        // lifetime into every caller. Uploads are capped at MaxUploadBytes, so this is bounded.
        var buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer, ct);
        response.Dispose();
        buffer.Position = 0;
        return buffer;
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        await _s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _options.Bucket,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                // The SDK closes the input stream once it has uploaded it. A storage helper has
                // no business disposing a stream it was handed — the caller still owns it, and
                // silently closing it turns a later read into an ObjectDisposedException.
                AutoCloseStream = false,
                // Payload signing stays on: the SDK refuses to skip it over plain HTTP, which is
                // exactly what the dev MinIO speaks. The cost is one extra hash of an image we
                // have already decoded and re-encoded in memory.
            },
            ct);
    }

    public async Task<ObjectHead?> HeadAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var response = await _s3.GetObjectMetadataAsync(_options.Bucket, key, ct);
            return new ObjectHead(response.ContentLength, response.Headers.ContentType);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task DeleteAsync(string key, CancellationToken ct = default) =>
        _s3.DeleteObjectAsync(_options.Bucket, key, ct);

    public async Task EnsureBucketAsync(CancellationToken ct = default)
    {
        try
        {
            var buckets = await _s3.ListBucketsAsync(ct);
            if (buckets.Buckets.Any(b => b.BucketName == _options.Bucket))
            {
                return;
            }

            await _s3.PutBucketAsync(new PutBucketRequest { BucketName = _options.Bucket }, ct);
            _logger.LogInformation("Created media bucket {Bucket}", _options.Bucket);
        }
        catch (Exception exception)
        {
            // Never fatal: the API must still start and serve everything that does not touch
            // media, so a storage outage degrades photos rather than the whole product.
            _logger.LogWarning(exception, "Could not ensure media bucket {Bucket} exists", _options.Bucket);
        }
    }

    public void Dispose()
    {
        if (!ReferenceEquals(_signer, _s3))
        {
            _signer.Dispose();
        }

        _s3.Dispose();
    }
}

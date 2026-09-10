using System.Collections.Concurrent;
using PamyatRyadom.Api.Services.Media;

namespace PamyatRyadom.Api.Tests.Infrastructure;

/// <summary>
/// Object storage held in a dictionary.
///
/// Stands in for S3 so tests about <em>who may see a photo</em> do not need a bucket, credentials
/// or a network. It is deliberately faithful about the parts those tests depend on — a key that
/// was never written reads back as missing, and a deleted key stops resolving — and deliberately
/// naive about the rest: the "presigned" URLs are strings nobody fetches.
/// </summary>
public sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, (byte[] Bytes, string ContentType)> _objects = new();

    public IReadOnlyCollection<string> Keys => _objects.Keys.ToArray();

    public string CreateUploadUrl(string key, string contentType) =>
        $"https://storage.test/upload/{Uri.EscapeDataString(key)}";

    public string CreateDownloadUrl(string key) =>
        $"https://storage.test/download/{Uri.EscapeDataString(key)}";

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default) =>
        _objects.TryGetValue(key, out var stored)
            ? Task.FromResult<Stream>(new MemoryStream(stored.Bytes, writable: false))
            : throw new FileNotFoundException($"No object at '{key}'.");

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        _objects[key] = (buffer.ToArray(), contentType);
    }

    public Task<ObjectHead?> HeadAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(_objects.TryGetValue(key, out var stored)
            ? new ObjectHead(stored.Bytes.LongLength, stored.ContentType)
            : null);

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task EnsureBucketAsync(CancellationToken ct = default) => Task.CompletedTask;
}

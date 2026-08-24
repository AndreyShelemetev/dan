using System.Text.Json.Serialization;

namespace PamyatRyadom.Api.Dtos.Common;

/// <summary>Non-generic marker so filters (e.g. RequireRoleFilter) can attach/merge <see cref="Meta"/>
/// onto whatever ApiResponse&lt;T&gt; a controller action already returned, without knowing T.</summary>
public interface IApiResponse
{
    object? Meta { get; set; }
}

public sealed class ApiResponse<T> : IApiResponse
{
    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("meta")]
    public object? Meta { get; set; }

    [JsonPropertyName("errors")]
    public IReadOnlyList<ApiError>? Errors { get; init; }

    public static ApiResponse<T> Ok(T data, object? meta = null) =>
        new() { Data = data, Meta = meta };

    public static ApiResponse<T> Fail(params ApiError[] errors) =>
        new() { Errors = errors };
}

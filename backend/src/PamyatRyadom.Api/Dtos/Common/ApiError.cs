using System.Text.Json.Serialization;

namespace PamyatRyadom.Api.Dtos.Common;

public sealed class ApiError
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("details")]
    public object? Details { get; init; }

    public static ApiError Validation(string message, object? details = null) =>
        new() { Code = "validation_error", Message = message, Details = details };

    public static ApiError NotFound(string message, object? details = null) =>
        new() { Code = "not_found", Message = message, Details = details };

    public static ApiError Of(string code, string message, object? details = null) =>
        new() { Code = code, Message = message, Details = details };
}

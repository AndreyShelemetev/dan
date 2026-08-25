using Microsoft.AspNetCore.Http;
using PamyatRyadom.Api.Dtos.Common;

namespace PamyatRyadom.Api.Services.Common;

/// <summary>
/// Outcome of a service call: either data, or an HTTP status plus errors for the envelope.
///
/// Deliberately module-agnostic. <c>AuthServiceResult&lt;T&gt;</c> predates this type and is
/// identical in shape; it stays where it is for now because converging the two would touch
/// the whole Identity module and its tests for no behavioural gain. New modules use this one
/// so they never have to reference the Auth namespace for a piece of plumbing.
/// </summary>
public sealed class ServiceResult<T>
{
    public bool Succeeded { get; init; }
    public T? Data { get; init; }
    public IReadOnlyList<ApiError> Errors { get; init; } = Array.Empty<ApiError>();
    public int StatusCode { get; init; } = StatusCodes.Status400BadRequest;

    public static ServiceResult<T> Ok(T data) =>
        new() { Succeeded = true, Data = data, StatusCode = StatusCodes.Status200OK };

    public static ServiceResult<T> Created(T data) =>
        new() { Succeeded = true, Data = data, StatusCode = StatusCodes.Status201Created };

    public static ServiceResult<T> Fail(int statusCode, params ApiError[] errors) =>
        new() { Succeeded = false, Errors = errors, StatusCode = statusCode };

    /// <summary>Used wherever revealing the difference between "does not exist" and "exists but
    /// is not yours" would leak the existence of another family's record.</summary>
    public static ServiceResult<T> NotFound(string message = "Запись не найдена.") =>
        Fail(StatusCodes.Status404NotFound, ApiError.NotFound(message));

    public static ServiceResult<T> Forbidden(string message = "Недостаточно прав.") =>
        Fail(StatusCodes.Status403Forbidden, ApiError.Of("forbidden", message));

    public static ServiceResult<T> Validation(string message, object? details = null) =>
        Fail(StatusCodes.Status400BadRequest, ApiError.Validation(message, details));
}

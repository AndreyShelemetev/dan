using Microsoft.AspNetCore.Http;
using PamyatRyadom.Api.Dtos.Common;

namespace PamyatRyadom.Api.Services.Auth;

public sealed class AuthServiceResult<T>
{
    public bool Succeeded { get; init; }
    public T? Data { get; init; }
    public IReadOnlyList<ApiError> Errors { get; init; } = Array.Empty<ApiError>();
    public int StatusCode { get; init; } = StatusCodes.Status400BadRequest;

    public static AuthServiceResult<T> Ok(T data) =>
        new() { Succeeded = true, Data = data, StatusCode = StatusCodes.Status200OK };

    public static AuthServiceResult<T> Fail(int statusCode, params ApiError[] errors) =>
        new() { Succeeded = false, Errors = errors, StatusCode = statusCode };
}

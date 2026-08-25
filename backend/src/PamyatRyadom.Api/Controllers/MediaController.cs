using Microsoft.AspNetCore.Mvc;
using PamyatRyadom.Api.Dtos.Common;
using PamyatRyadom.Api.Dtos.Media;
using PamyatRyadom.Api.Services.Auth;
using PamyatRyadom.Api.Services.Common;
using PamyatRyadom.Api.Services.Media;

namespace PamyatRyadom.Api.Controllers;

/// <summary>
/// Photo upload and access.
///
/// The bytes never pass through this API: the client is handed a short-lived presigned URL and
/// uploads straight to the private bucket, then calls <c>complete</c> so the file can be checked
/// and normalised. Reading works the same way in reverse — a signed link, minted per response,
/// after the caller's right to the owning record has been verified.
/// </summary>
[ApiController]
[Route("api/v1/media")]
[RequireRole]
public sealed class MediaController : AuthorizedControllerBase
{
    private readonly IMediaService _media;

    public MediaController(IMediaService media) => _media = media;

    [HttpPost("upload-sessions")]
    public async Task<ActionResult<ApiResponse<UploadSessionDto>>> CreateUploadSession(
        [FromBody] CreateUploadSessionDto dto, CancellationToken ct) =>
        Envelope(await _media.CreateUploadSessionAsync(CurrentUserId, dto, ct));

    [HttpPost("{id:long}/complete")]
    public async Task<ActionResult<ApiResponse<MediaAssetDto>>> Complete(long id, CancellationToken ct) =>
        Envelope(await _media.CompleteUploadAsync(CurrentUserId, id, ct));

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MediaAssetDto>>>> List(
        [FromQuery] string ownerType, [FromQuery] long ownerId, CancellationToken ct) =>
        Envelope(await _media.ListAsync(CurrentUserId, ownerType, ownerId, ct));

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken ct) =>
        Envelope(await _media.DeleteAsync(CurrentUserId, id, ct));

    private ActionResult<ApiResponse<T>> Envelope<T>(ServiceResult<T> result) =>
        result.Succeeded && result.Data is not null
            ? StatusCode(result.StatusCode, ApiResponse<T>.Ok(result.Data))
            : StatusCode(result.StatusCode, ApiResponse<T>.Fail(result.Errors.ToArray()));
}

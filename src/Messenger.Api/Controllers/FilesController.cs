using Messenger.Domain.Dtos;
using Messenger.Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace Messenger.Api.Controllers;

[Route("api/v1/files")]
public sealed class FilesController(IFileStorageService files) : ApiControllerBase
{
    [HttpPost("images")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<AttachmentDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> UploadImage(IFormFile? file, CancellationToken ct)
    {
        var f = RequireFile(file);
        await using var stream = await OpenSeekableAsync(f, ct);
        return Created(await files.SaveImageAsync(CurrentUserId, stream, f.FileName, f.ContentType, ct));
    }

    [HttpGet("{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid attachmentId, CancellationToken ct)
    {
        var file = await files.OpenReadAsync(CurrentUserId, attachmentId, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(file.Content, file.ContentType); // FileStreamResult disposes the stream
    }
}

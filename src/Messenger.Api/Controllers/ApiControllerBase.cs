using Messenger.Api.Infrastructure;
using Messenger.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Messenger.Api.Controllers;

[ApiController]
[Route("api/v1")]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid CurrentUserId => User.GetUserId();

    protected static IFormFile RequireFile(IFormFile? file)
        => file is { Length: > 0 } ? file : throw new ValidationException("file", "A non-empty 'file' part is required.");

    /// <summary>Returns a seekable stream (Domain inspects magic bytes and rewinds).</summary>
    protected static async Task<Stream> OpenSeekableAsync(IFormFile file, CancellationToken ct)
    {
        var s = file.OpenReadStream();
        if (s.CanSeek) return s;
        var ms = new MemoryStream();
        await using (s) await s.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }

    protected ObjectResult Created<T>(T body) => StatusCode(StatusCodes.Status201Created, body);
}

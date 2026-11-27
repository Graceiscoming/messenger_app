using Messenger.Domain.Dtos;
using Messenger.Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace Messenger.Api.Controllers;

public sealed class MessagesController(IMessageService messages) : ApiControllerBase
{
    [HttpDelete("messages/{id:long}")]
    public async Task<IActionResult> Unsend(long id, CancellationToken ct)
    {
        await messages.UnsendAsync(CurrentUserId, id, ct);
        return NoContent();
    }

    [HttpGet("stickers")]
    public async Task<ActionResult<IReadOnlyList<StickerPackDto>>> Stickers(CancellationToken ct)
        => Ok(await messages.ListStickersAsync(ct));
}

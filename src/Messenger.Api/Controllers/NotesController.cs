using Messenger.Api.Contracts;
using Messenger.Domain.Dtos;
using Messenger.Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace Messenger.Api.Controllers;

/// <summary>Instagram-style notes (see docs/decisions.md, "Notes").</summary>
[Route("api/v1/notes")]
public sealed class NotesController(INoteService notes) : ApiControllerBase
{
    /// <summary>The caller's own active note first, then friends' active notes.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NoteDto>>> List(CancellationToken ct)
        => Ok(await notes.ListAsync(CurrentUserId, ct));

    /// <summary>Posts (or replaces) the caller's note.</summary>
    [HttpPut("me")]
    public async Task<ActionResult<NoteDto>> Set(SetNoteRequest request, CancellationToken ct)
        => Ok(await notes.SetAsync(CurrentUserId, request.Content, ct));

    [HttpDelete("me")]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        await notes.DeleteAsync(CurrentUserId, ct);
        return NoContent();
    }

    /// <summary>Replies to a friend's note; the reply is a message in the direct chat.</summary>
    [HttpPost("{id:guid}/replies")]
    [ProducesResponseType<MessageDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Reply(Guid id, NoteReplyRequest request, CancellationToken ct)
        => Created(await notes.ReplyAsync(CurrentUserId, id, request.Content, ct));
}

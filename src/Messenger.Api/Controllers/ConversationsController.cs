using Messenger.Api.Contracts;
using Messenger.Domain.Dtos;
using Messenger.Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace Messenger.Api.Controllers;

[Route("api/v1/conversations")]
public sealed class ConversationsController(IConversationService conversations, IMessageService messages) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConversationDto>>> List(CancellationToken ct)
        => Ok(await conversations.ListAsync(CurrentUserId, ct));

    [HttpPost("direct")]
    public async Task<ActionResult<ConversationDto>> Direct(DirectConversationRequest request, CancellationToken ct)
        => await conversations.GetOrCreateDirectAsync(CurrentUserId, request.FriendId, ct);

    [HttpPost("group")]
    [ProducesResponseType<ConversationDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateGroup(CreateGroupDto dto, CancellationToken ct)
        => Created(await conversations.CreateGroupAsync(CurrentUserId, dto, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ConversationDto>> Get(Guid id, CancellationToken ct)
        => await conversations.GetAsync(CurrentUserId, id, ct);

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ConversationDto>> Update(Guid id, UpdateGroupDto dto, CancellationToken ct)
        => await conversations.UpdateGroupAsync(CurrentUserId, id, dto, ct);

    [HttpPut("{id:guid}/avatar")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ConversationDto>> SetAvatar(Guid id, IFormFile? file, CancellationToken ct)
    {
        var f = RequireFile(file);
        await using var stream = await OpenSeekableAsync(f, ct);
        return await conversations.SetGroupAvatarAsync(CurrentUserId, id, stream, f.FileName, f.ContentType, ct);
    }

    [HttpPost("{id:guid}/members")]
    public async Task<ActionResult<ConversationDto>> AddMembers(Guid id, AddMembersRequest request, CancellationToken ct)
        => await conversations.AddMembersAsync(CurrentUserId, id, request.UserIds, ct);

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        await conversations.RemoveMemberAsync(CurrentUserId, id, userId, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/members/{userId:guid}/role")]
    public async Task<IActionResult> SetRole(Guid id, Guid userId, SetRoleRequest request, CancellationToken ct)
    {
        await conversations.SetRoleAsync(CurrentUserId, id, userId, request.Role, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        await conversations.LeaveAsync(CurrentUserId, id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, MarkReadRequest request, CancellationToken ct)
    {
        await conversations.MarkReadAsync(CurrentUserId, id, request.MessageId, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<ActionResult<IReadOnlyList<MessageDto>>> History(
        Guid id, [FromQuery] long? beforeId, [FromQuery] int limit = 30, CancellationToken ct = default)
        => Ok(await messages.GetHistoryAsync(CurrentUserId, id, beforeId, limit, ct));

    [HttpPost("{id:guid}/messages")]
    [ProducesResponseType<MessageDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Send(Guid id, SendMessageDto dto, CancellationToken ct)
        => Created(await messages.SendAsync(CurrentUserId, id, dto, ct));
}

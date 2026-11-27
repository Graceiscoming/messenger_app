using Messenger.Api.Contracts;
using Messenger.Domain.Dtos;
using Messenger.Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace Messenger.Api.Controllers;

[Route("api/v1/friends")]
public sealed class FriendsController(IFriendService friends) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FriendDto>>> List(CancellationToken ct)
        => Ok(await friends.ListFriendsAsync(CurrentUserId, ct));

    [HttpDelete("{friendId:guid}")]
    public async Task<IActionResult> Remove(Guid friendId, CancellationToken ct)
    {
        await friends.RemoveFriendAsync(CurrentUserId, friendId, ct);
        return NoContent();
    }

    [HttpGet("requests")]
    public async Task<ActionResult<FriendRequestsDto>> Requests(CancellationToken ct)
        => await friends.ListRequestsAsync(CurrentUserId, ct);

    [HttpPost("requests")]
    [ProducesResponseType<FriendshipDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Send(SendFriendRequestRequest request, CancellationToken ct)
        => Created(await friends.SendRequestByUsernameAsync(CurrentUserId, request.Username, ct));

    [HttpPost("requests/{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct)
    {
        await friends.AcceptAsync(CurrentUserId, id, ct);
        return NoContent();
    }

    [HttpPost("requests/{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id, CancellationToken ct)
    {
        await friends.DeclineAsync(CurrentUserId, id, ct);
        return NoContent();
    }

    [HttpDelete("requests/{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await friends.CancelAsync(CurrentUserId, id, ct);
        return NoContent();
    }
}

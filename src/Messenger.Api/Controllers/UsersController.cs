using Messenger.Domain.Dtos;
using Messenger.Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace Messenger.Api.Controllers;

[Route("api/v1/users")]
public sealed class UsersController(IUserService users) : ApiControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe(CancellationToken ct)
        => await users.GetProfileAsync(CurrentUserId, ct);

    [HttpPut("me")]
    public async Task<ActionResult<UserDto>> UpdateMe(UpdateProfileDto dto, CancellationToken ct)
        => await users.UpdateProfileAsync(CurrentUserId, dto, ct);

    [HttpPut("me/avatar")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UserDto>> SetAvatar(IFormFile? file, CancellationToken ct)
    {
        var f = RequireFile(file);
        await using var stream = await OpenSeekableAsync(f, ct);
        return await users.SetAvatarAsync(CurrentUserId, stream, f.FileName, f.ContentType, ct);
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> Search([FromQuery] string? username, CancellationToken ct)
        => Ok(await users.SearchAsync(CurrentUserId, username ?? string.Empty, ct));

    [HttpPost("{id:guid}/block")]
    public async Task<IActionResult> Block(Guid id, CancellationToken ct)
    {
        await users.BlockAsync(CurrentUserId, id, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/block")]
    public async Task<IActionResult> Unblock(Guid id, CancellationToken ct)
    {
        await users.UnblockAsync(CurrentUserId, id, ct);
        return NoContent();
    }
}

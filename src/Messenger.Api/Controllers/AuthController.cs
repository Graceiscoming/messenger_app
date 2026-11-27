using Messenger.Api.Contracts;
using Messenger.Domain.Dtos;
using Messenger.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Messenger.Api.Controllers;

[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(IAuthService auth) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<AuthResultDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterDto dto, CancellationToken ct)
        => Created(await auth.RegisterAsync(dto, ct));

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResultDto>> Login(LoginDto dto, CancellationToken ct)
        => await auth.LoginAsync(dto, ct);

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResultDto>> Refresh(RefreshRequest request, CancellationToken ct)
        => await auth.RefreshAsync(request.RefreshToken, ct);

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(request.RefreshToken, ct);
        return NoContent();
    }
}

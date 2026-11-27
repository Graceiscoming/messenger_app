using System.Security.Claims;
using Messenger.Domain.Exceptions;

namespace Messenger.Api.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Reads the user id from the JWT "sub" claim (inbound claim mapping is disabled).</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : throw new UnauthorizedException("Invalid access token.");
    }
}

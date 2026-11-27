using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Messenger.Domain.Abstractions;
using Messenger.Domain.Entities;
using Messenger.Domain.Options;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Messenger.Api.Infrastructure;

public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : ITokenService
{
    private readonly JwtOptions _opt = options.Value;
    private readonly JwtSecurityTokenHandler _handler = new();

    public string CreateAccessToken(User user)
    {
        var now = clock.UtcNow;
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key)), SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        var token = new JwtSecurityToken(
            _opt.Issuer, _opt.Audience, claims,
            notBefore: now, expires: now.AddMinutes(_opt.AccessMinutes), signingCredentials: creds);
        return _handler.WriteToken(token);
    }

    public string CreateRefreshToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public string HashRefreshToken(string refreshToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken))).ToLowerInvariant();
}

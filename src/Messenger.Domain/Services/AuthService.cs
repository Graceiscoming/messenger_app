using Messenger.Domain.Abstractions;
using Messenger.Domain.Dtos;
using Messenger.Domain.Entities;
using Messenger.Domain.Exceptions;
using Microsoft.Extensions.Options;
using DomainOptions = Messenger.Domain.Options;

namespace Messenger.Domain.Services;

internal sealed class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher hasher,
    ITokenService tokenService,
    IUnitOfWork uow,
    IClock clock,
    IOptions<DomainOptions.JwtOptions> jwt,
    IOptions<DomainOptions.AuthOptions> auth) : IAuthService
{
    public async Task<AuthResultDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default)
    {
        Rules.Username(dto.Username);          // BR-1
        Rules.DisplayName(dto.DisplayName);
        Rules.Password(dto.Password);

        var normalized = User.Normalize(dto.Username);
        if (await users.FindByNormalizedUsernameAsync(normalized, ct) is not null)
            throw new ConflictException("Username is already taken.");

        var user = User.Create(dto.Username, dto.DisplayName, hasher.Hash(dto.Password), clock.UtcNow);
        await users.AddAsync(user, ct);
        var result = await IssueTokensAsync(user, ct);
        await uow.SaveChangesAsync(ct);
        return result;
    }

    public async Task<AuthResultDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        const string invalid = "Invalid username or password.";
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrEmpty(dto.Password))
            throw new UnauthorizedException(invalid);

        var user = await users.FindByNormalizedUsernameAsync(User.Normalize(dto.Username), ct)
                   ?? throw new UnauthorizedException(invalid);

        var now = clock.UtcNow;
        if (user.IsLockedOut(now))
            throw new UnauthorizedException("Account is temporarily locked. Try again later.");

        if (!hasher.Verify(user.PasswordHash, dto.Password))
        {
            user.RegisterFailedLogin(now, auth.Value.MaxFailedLogins, TimeSpan.FromMinutes(auth.Value.LockoutMinutes));
            await uow.SaveChangesAsync(ct);
            throw new UnauthorizedException(invalid);
        }

        user.RegisterSuccessfulLogin();
        var result = await IssueTokensAsync(user, ct);
        await uow.SaveChangesAsync(ct);
        return result;
    }

    public async Task<AuthResultDto> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new UnauthorizedException("Invalid refresh token.");

        var now = clock.UtcNow;
        var stored = await refreshTokens.FindByHashAsync(tokenService.HashRefreshToken(refreshToken), ct);
        if (stored is null || !stored.IsActive(now))
            throw new UnauthorizedException("Invalid refresh token.");

        var user = await users.FindByIdAsync(stored.UserId, ct)
                   ?? throw new UnauthorizedException("Invalid refresh token.");

        stored.Revoke(now); // rotation: each refresh token is single-use
        var result = await IssueTokensAsync(user, ct);
        await uow.SaveChangesAsync(ct);
        return result;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var stored = await refreshTokens.FindByHashAsync(tokenService.HashRefreshToken(refreshToken), ct);
        if (stored is null) return;
        stored.Revoke(clock.UtcNow);
        await uow.SaveChangesAsync(ct);
    }

    private async Task<AuthResultDto> IssueTokensAsync(User user, CancellationToken ct)
    {
        var raw = tokenService.CreateRefreshToken();
        var entity = RefreshToken.Create(user.Id, tokenService.HashRefreshToken(raw), clock.UtcNow,
            TimeSpan.FromDays(jwt.Value.RefreshDays));
        await refreshTokens.AddAsync(entity, ct);
        return new AuthResultDto(tokenService.CreateAccessToken(user), raw, jwt.Value.AccessMinutes * 60, user.ToDto());
    }
}

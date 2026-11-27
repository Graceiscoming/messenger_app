using Messenger.Domain.Dtos;
using Messenger.Domain.Exceptions;
using Messenger.Domain.Tests.TestKit;

namespace Messenger.Domain.Tests;

public class AuthServiceTests
{
    private readonly World w = new();

    private Task<AuthResultDto> Register(string username = "alice", string pw = "Passw0rd!")
        => w.Auth.RegisterAsync(new RegisterDto(username, "Alice", pw));

    [Fact]
    public async Task Register_ValidInput_HashesPasswordAndReturnsTokens()
    {
        var r = await Register();
        Assert.Equal("alice", r.User.Username);
        Assert.Equal(15 * 60, r.ExpiresIn);
        Assert.NotEmpty(r.RefreshToken);
        Assert.Equal("hash:Passw0rd!", w.Db.Users.Single().PasswordHash);   // never stored in clear text
        Assert.Single(w.Db.RefreshTokens);
    }

    [Fact]
    public async Task Register_DuplicateUsernameDifferentCase_ThrowsConflict_BR1()
    {
        await Register("Alice");
        await Assert.ThrowsAsync<ConflictException>(() => Register("aLICE"));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("abcdefghijklmnopqrstu")]   // 21
    [InlineData("bad name")]
    [InlineData("bad-name")]
    [InlineData("")]
    public async Task Register_InvalidUsername_ThrowsValidation_BR1(string username)
        => await Assert.ThrowsAsync<ValidationException>(() => Register(username));

    [Theory]
    [InlineData("short1")]
    [InlineData("onlyletters")]
    [InlineData("12345678")]
    public async Task Register_WeakPassword_ThrowsValidation(string pw)
        => await Assert.ThrowsAsync<ValidationException>(() => Register("alice", pw));

    [Fact]
    public async Task Login_WrongPassword_ThrowsUnauthorized()
    {
        await Register();
        await Assert.ThrowsAsync<UnauthorizedException>(() => w.Auth.LoginAsync(new LoginDto("alice", "wrong")));
    }

    [Fact]
    public async Task Login_UnknownUser_ThrowsUnauthorized()
        => await Assert.ThrowsAsync<UnauthorizedException>(() => w.Auth.LoginAsync(new LoginDto("nobody", "x")));

    [Fact]
    public async Task Login_UsernameIsCaseInsensitive()
    {
        await Register("Alice");
        var r = await w.Auth.LoginAsync(new LoginDto("ALICE", "Passw0rd!"));
        Assert.Equal("Alice", r.User.Username);
    }

    [Fact]
    public async Task Login_FiveFailures_LocksAccountEvenForCorrectPassword_UC02()
    {
        await Register();
        for (var i = 0; i < 5; i++)
            await Assert.ThrowsAsync<UnauthorizedException>(() => w.Auth.LoginAsync(new LoginDto("alice", "wrong")));

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => w.Auth.LoginAsync(new LoginDto("alice", "Passw0rd!")));
        Assert.Contains("locked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_FourFailures_DoesNotLock()
    {
        await Register();
        for (var i = 0; i < 4; i++)
            await Assert.ThrowsAsync<UnauthorizedException>(() => w.Auth.LoginAsync(new LoginDto("alice", "wrong")));
        var r = await w.Auth.LoginAsync(new LoginDto("alice", "Passw0rd!"));
        Assert.NotEmpty(r.AccessToken);
    }

    [Fact]
    public async Task Login_AfterLockoutExpires_Succeeds()
    {
        await Register();
        for (var i = 0; i < 5; i++)
            await Assert.ThrowsAsync<UnauthorizedException>(() => w.Auth.LoginAsync(new LoginDto("alice", "wrong")));

        w.Clock.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));
        var r = await w.Auth.LoginAsync(new LoginDto("alice", "Passw0rd!"));
        Assert.NotEmpty(r.AccessToken);
    }

    [Fact]
    public async Task Login_SuccessResetsFailureCounter()
    {
        await Register();
        for (var i = 0; i < 4; i++)
            await Assert.ThrowsAsync<UnauthorizedException>(() => w.Auth.LoginAsync(new LoginDto("alice", "wrong")));
        await w.Auth.LoginAsync(new LoginDto("alice", "Passw0rd!"));
        for (var i = 0; i < 4; i++)
            await Assert.ThrowsAsync<UnauthorizedException>(() => w.Auth.LoginAsync(new LoginDto("alice", "wrong")));
        await w.Auth.LoginAsync(new LoginDto("alice", "Passw0rd!")); // still not locked
    }

    [Fact]
    public async Task Refresh_RotatesToken_OldTokenBecomesInvalid()
    {
        var first = await Register();
        var second = await w.Auth.RefreshAsync(first.RefreshToken);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        await Assert.ThrowsAsync<UnauthorizedException>(() => w.Auth.RefreshAsync(first.RefreshToken));
    }

    [Fact]
    public async Task Refresh_ExpiredToken_ThrowsUnauthorized()
    {
        var first = await Register();
        w.Clock.Advance(TimeSpan.FromDays(7).Add(TimeSpan.FromMinutes(1)));
        await Assert.ThrowsAsync<UnauthorizedException>(() => w.Auth.RefreshAsync(first.RefreshToken));
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var first = await Register();
        await w.Auth.LogoutAsync(first.RefreshToken);
        await Assert.ThrowsAsync<UnauthorizedException>(() => w.Auth.RefreshAsync(first.RefreshToken));
    }

    [Fact]
    public async Task Logout_UnknownToken_IsIdempotent() => await w.Auth.LogoutAsync("nope");
}

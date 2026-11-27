namespace Messenger.Domain.Entities;

public sealed class RefreshToken
{
    private RefreshToken() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTime now, TimeSpan lifetime) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TokenHash = tokenHash,
        CreatedAt = now,
        ExpiresAt = now + lifetime,
    };

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;
    public void Revoke(DateTime now) => RevokedAt ??= now;
}

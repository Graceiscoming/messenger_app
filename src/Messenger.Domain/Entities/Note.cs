namespace Messenger.Domain.Entities;

/// <summary>
/// Short status note shown to friends above the user's avatar (Instagram-style). One per user; expires after 24 h.
/// </summary>
public sealed class Note
{
    public const int MaxLength = 60;
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private Note() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Content { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    /// <summary>Navigation; repositories load it for reads.</summary>
    public User? User { get; private set; }

    public static Note Create(Guid userId, string content, DateTime now) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, Content = content, CreatedAt = now, ExpiresAt = now + Lifetime,
    };

    public bool IsActive(DateTime now) => ExpiresAt > now;

    public void AttachUser(User user) => User = user;
}

namespace Messenger.Domain.Entities;

public sealed class User
{
    private User() { }

    public Guid Id { get; private set; }
    public string Username { get; private set; } = null!;
    /// <summary>Lower-case username; the unique index lives on this column.</summary>
    public string NormalizedUsername { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string? StatusMessage { get; private set; }
    public Guid? AvatarAttachmentId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastSeenAt { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTime? LockoutUntil { get; private set; }

    public static string Normalize(string username) => username.Trim().ToLowerInvariant();

    public static User Create(string username, string displayName, string passwordHash, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        Username = username.Trim(),
        NormalizedUsername = Normalize(username),
        DisplayName = displayName.Trim(),
        PasswordHash = passwordHash,
        CreatedAt = now,
    };

    public bool IsLockedOut(DateTime now) => LockoutUntil.HasValue && LockoutUntil.Value > now;

    /// <summary>Counts a failed login; locks the account once <paramref name="maxFailed"/> is reached.</summary>
    public void RegisterFailedLogin(DateTime now, int maxFailed, TimeSpan lockoutDuration)
    {
        if (LockoutUntil.HasValue && LockoutUntil.Value <= now)
        {
            LockoutUntil = null;
            FailedLoginCount = 0;
        }
        FailedLoginCount++;
        if (FailedLoginCount >= maxFailed)
        {
            LockoutUntil = now + lockoutDuration;
            FailedLoginCount = 0;
        }
    }

    public void RegisterSuccessfulLogin()
    {
        FailedLoginCount = 0;
        LockoutUntil = null;
    }

    public void UpdateProfile(string displayName, string? statusMessage)
    {
        DisplayName = displayName.Trim();
        StatusMessage = string.IsNullOrWhiteSpace(statusMessage) ? null : statusMessage.Trim();
    }

    public void SetAvatar(Guid? attachmentId) => AvatarAttachmentId = attachmentId;
    public void SetLastSeen(DateTime now) => LastSeenAt = now;
}

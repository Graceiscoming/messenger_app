using Messenger.Domain.Dtos;
using Messenger.Domain.Entities;

namespace Messenger.Domain.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string passwordHash, string password);
}

public interface ITokenService
{
    string CreateAccessToken(User user);
    /// <summary>Opaque random token handed to the client.</summary>
    string CreateRefreshToken();
    /// <summary>Deterministic hash (e.g. SHA-256 base64) stored in refresh_tokens.token_hash.</summary>
    string HashRefreshToken(string refreshToken);
}

public interface IPresenceTracker
{
    bool IsOnline(Guid userId);
    IReadOnlyCollection<Guid> GetOnlineUsers(IEnumerable<Guid> userIds);
}

/// <summary>Result of inspecting image bytes. Extension includes the dot (".png").</summary>
public sealed record ImageInfo(string ContentType, string Extension, int? Width, int? Height);

public interface IImageInspector
{
    /// <summary>
    /// Detects jpeg/png/webp/gif by magic bytes and reads dimensions. Returns null for anything else.
    /// The stream is seekable and positioned at 0; the implementation may move it (Domain resets it afterwards).
    /// </summary>
    ImageInfo? Inspect(Stream stream);
}

public interface IFileStore
{
    Task SaveAsync(Stream content, string storedName, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string storedName, CancellationToken ct = default);
    /// <summary>Must not throw if the file does not exist.</summary>
    Task DeleteAsync(string storedName, CancellationToken ct = default);
}

public static class FriendEventTypes
{
    public const string Received = "Received";
    public const string Accepted = "Accepted";
}

/// <summary>Always called AFTER the change has been saved.</summary>
public interface IRealtimeNotifier
{
    /// <summary>-> client event MessageReceived. Sent to each user's personal group.</summary>
    Task NotifyMessageReceived(IEnumerable<Guid> userIds, MessageDto message, CancellationToken ct = default);
    /// <summary>-> MessageUnsent { conversationId, messageId } to the conversation group.</summary>
    Task NotifyMessageUnsent(Guid conversationId, long messageId, CancellationToken ct = default);
    /// <summary>type is FriendEventTypes.Received (-> FriendRequestReceived) or Accepted (-> FriendRequestAccepted).</summary>
    Task NotifyFriendEvent(Guid userId, string type, FriendshipDto friendship, CancellationToken ct = default);
    /// <summary>-> FriendRemoved { userId = friendId } to recipientId.</summary>
    Task NotifyFriendRemoved(Guid recipientId, Guid friendId, CancellationToken ct = default);
    /// <summary>-> ConversationChanged, personalised (unreadCount/isReadOnly) for userId.</summary>
    Task NotifyConversationChanged(Guid userId, ConversationDto conversation, CancellationToken ct = default);
    /// <summary>Hub must add/remove the users' live connections to/from the conv:{id} group.</summary>
    Task NotifyMembershipChanged(Guid conversationId, IReadOnlyCollection<Guid> addedUserIds, IReadOnlyCollection<Guid> removedUserIds, CancellationToken ct = default);
    /// <summary>-> MessageRead { conversationId, userId, messageId } to the conversation group.</summary>
    Task NotifyRead(Guid conversationId, Guid userId, long messageId, CancellationToken ct = default);
}

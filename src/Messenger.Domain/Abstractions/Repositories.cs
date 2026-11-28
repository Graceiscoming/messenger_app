using Messenger.Domain.Entities;

namespace Messenger.Domain.Abstractions;

/// <summary>
/// Read model for one conversation as seen by one user (single-query projection in Data).
/// Conversation.Members must be loaded with each member's User; LastMessage with Sender and Sticker.
/// </summary>
public sealed record ConversationSummary(Conversation Conversation, Message? LastMessage, int UnreadCount);

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    /// <summary>
    /// Runs <paramref name="work"/> inside one database transaction. Commits if it completes, rolls back if it throws.
    /// The delegate runs once (no retry-on-failure: it mutates tracked entities). Nested calls join the open transaction.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default);
}

public interface IUserRepository
{
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Argument is already lower-cased (User.Normalize).</summary>
    Task<User?> FindByNormalizedUsernameAsync(string normalizedUsername, CancellationToken ct = default);
    Task<IReadOnlyList<User>> FindByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    /// <summary>Prefix match on NormalizedUsername, ordered by username, at most <paramref name="limit"/> rows.</summary>
    Task<IReadOnlyList<User>> SearchByUsernamePrefixAsync(string normalizedPrefix, int limit, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
}

public interface IFriendshipRepository
{
    /// <summary>Either direction; Requester and Addressee loaded; tracked.</summary>
    Task<Friendship?> FindBetweenAsync(Guid userA, Guid userB, CancellationToken ct = default);
    Task<Friendship?> FindByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Accepted friendships involving the user (users loaded).</summary>
    Task<IReadOnlyList<Friendship>> ListAcceptedAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Pending friendships involving the user, both incoming and outgoing (users loaded).</summary>
    Task<IReadOnlyList<Friendship>> ListPendingAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Friendship friendship, CancellationToken ct = default);
    void Remove(Friendship friendship);
}

public interface IUserBlockRepository
{
    Task<UserBlock?> FindAsync(Guid blockerId, Guid blockedId, CancellationToken ct = default);
    /// <summary>True if either user has blocked the other.</summary>
    Task<bool> ExistsBetweenAsync(Guid userA, Guid userB, CancellationToken ct = default);
    /// <summary>Ids of users who have blocked <paramref name="userId"/>.</summary>
    Task<IReadOnlyList<Guid>> ListBlockerIdsAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Ids of users related by a block in either direction.</summary>
    Task<IReadOnlyList<Guid>> ListRelatedUserIdsAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(UserBlock block, CancellationToken ct = default);
    void Remove(UserBlock block);
}

public interface IConversationRepository
{
    /// <summary>Tracked; Members (including left ones) loaded with their User.</summary>
    Task<Conversation?> FindByIdWithMembersAsync(Guid id, CancellationToken ct = default);
    Task<Conversation?> FindDirectAsync(string directKey, CancellationToken ct = default);
    /// <summary>directKey -> conversation id for the keys that exist.</summary>
    Task<IReadOnlyDictionary<string, Guid>> GetDirectConversationIdsAsync(IEnumerable<string> directKeys, CancellationToken ct = default);
    /// <summary>
    /// Conversations where the user is an active member, newest activity first
    /// (LastMessageAt desc, falling back to CreatedAt), in one query incl. last message and unread count.
    /// Unread = messages with Id > member.LastReadMessageId (or all if null), SenderId != user, UnsentAt == null.
    /// </summary>
    Task<IReadOnlyList<ConversationSummary>> ListForUserAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Same projection for one conversation; null if the user is not an active member.</summary>
    Task<ConversationSummary?> GetSummaryAsync(Guid userId, Guid conversationId, CancellationToken ct = default);
    /// <summary>True if the user is an active (not left/removed) member; must not load the whole conversation.</summary>
    Task<bool> IsActiveMemberAsync(Guid conversationId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> ListConversationIdsForUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Conversation conversation, CancellationToken ct = default);
}

public interface IMessageRepository
{
    Task AddAsync(Message message, CancellationToken ct = default);
    /// <summary>Tracked; Sender, Sticker, ReplyTo and ReplyTo.Sender loaded.</summary>
    Task<Message?> FindByIdAsync(long id, CancellationToken ct = default);
    /// <summary>Newest first (Id desc), Id &lt; beforeId when given. Same includes as FindByIdAsync.</summary>
    Task<IReadOnlyList<Message>> GetPageAsync(Guid conversationId, long? beforeId, int limit, CancellationToken ct = default);
    /// <summary>Conversations containing a (not unsent) message that references the attachment.</summary>
    Task<IReadOnlyList<Guid>> ListConversationIdsByAttachmentAsync(Guid attachmentId, CancellationToken ct = default);
}

public interface IAttachmentRepository
{
    Task AddAsync(Attachment attachment, CancellationToken ct = default);
    Task<Attachment?> FindByIdAsync(Guid id, CancellationToken ct = default);
    void Remove(Attachment attachment);
    /// <summary>True if any user or conversation currently uses the attachment as its avatar.</summary>
    Task<bool> IsAvatarAsync(Guid attachmentId, CancellationToken ct = default);
    /// <summary>True if any message (any type, unsent or not), user avatar or conversation avatar references the attachment.</summary>
    Task<bool> IsReferencedAsync(Guid attachmentId, CancellationToken ct = default);
}

public interface IStickerRepository
{
    /// <summary>All packs with their stickers.</summary>
    Task<IReadOnlyList<StickerPack>> ListPacksAsync(CancellationToken ct = default);
    Task<Sticker?> FindByIdAsync(int id, CancellationToken ct = default);
}

public interface INoteRepository
{
    /// <summary>Tracked; User loaded.</summary>
    Task<Note?> FindByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>All notes of the user (expired too), tracked: used to replace or delete them.</summary>
    Task<IReadOnlyList<Note>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Notes with ExpiresAt &gt; now for the given users, User loaded, newest first.</summary>
    Task<IReadOnlyList<Note>> ListActiveByUsersAsync(IReadOnlyCollection<Guid> userIds, DateTime now, CancellationToken ct = default);
    Task AddAsync(Note note, CancellationToken ct = default);
    void Remove(Note note);
}

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    /// <summary>Tracked.</summary>
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default);
}

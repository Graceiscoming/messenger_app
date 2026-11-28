using Messenger.Domain.Dtos;
using Messenger.Domain.Enums;

namespace Messenger.Domain.Services;

public interface IAuthService
{
    Task<AuthResultDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default);
    Task<AuthResultDto> LoginAsync(LoginDto dto, CancellationToken ct = default);
    Task<AuthResultDto> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
}

public interface IUserService
{
    Task<UserDto> GetProfileAsync(Guid id, CancellationToken ct = default);
    Task<UserDto> UpdateProfileAsync(Guid id, UpdateProfileDto dto, CancellationToken ct = default);
    Task<UserDto> SetAvatarAsync(Guid me, Stream content, string fileName, string contentType, CancellationToken ct = default);
    Task<IReadOnlyList<UserDto>> SearchAsync(Guid me, string username, CancellationToken ct = default);
    Task BlockAsync(Guid me, Guid target, CancellationToken ct = default);
    Task UnblockAsync(Guid me, Guid target, CancellationToken ct = default);
    /// <summary>Called by the hub on disconnect (sets LastSeenAt).</summary>
    Task MarkSeenAsync(Guid me, CancellationToken ct = default);
}

public interface IFriendService
{
    Task<FriendshipDto> SendRequestAsync(Guid me, Guid target, CancellationToken ct = default);
    Task<FriendshipDto> SendRequestByUsernameAsync(Guid me, string username, CancellationToken ct = default);
    Task AcceptAsync(Guid me, Guid friendshipId, CancellationToken ct = default);
    Task DeclineAsync(Guid me, Guid friendshipId, CancellationToken ct = default);
    Task CancelAsync(Guid me, Guid friendshipId, CancellationToken ct = default);
    Task RemoveFriendAsync(Guid me, Guid friendId, CancellationToken ct = default);
    Task<IReadOnlyList<FriendDto>> ListFriendsAsync(Guid me, CancellationToken ct = default);
    Task<FriendRequestsDto> ListRequestsAsync(Guid me, CancellationToken ct = default);
    Task<bool> AreFriendsAsync(Guid a, Guid b, CancellationToken ct = default);
    /// <summary>Ids of accepted friends (hub uses it for PresenceChanged fan-out).</summary>
    Task<IReadOnlyList<Guid>> GetFriendIdsAsync(Guid me, CancellationToken ct = default);
}

public interface IConversationService
{
    Task<IReadOnlyList<ConversationDto>> ListAsync(Guid me, CancellationToken ct = default);
    Task<ConversationDto> GetAsync(Guid me, Guid id, CancellationToken ct = default);
    Task<ConversationDto> GetOrCreateDirectAsync(Guid me, Guid friendId, CancellationToken ct = default);
    Task<ConversationDto> CreateGroupAsync(Guid me, CreateGroupDto dto, CancellationToken ct = default);
    Task<ConversationDto> UpdateGroupAsync(Guid me, Guid id, UpdateGroupDto dto, CancellationToken ct = default);
    Task<ConversationDto> SetGroupAvatarAsync(Guid me, Guid id, Stream content, string fileName, string contentType, CancellationToken ct = default);
    Task<ConversationDto> AddMembersAsync(Guid me, Guid id, IReadOnlyList<Guid> userIds, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid me, Guid id, Guid target, CancellationToken ct = default);
    Task LeaveAsync(Guid me, Guid id, CancellationToken ct = default);
    Task SetRoleAsync(Guid me, Guid id, Guid target, MemberRole role, CancellationToken ct = default);
    Task MarkReadAsync(Guid me, Guid id, long messageId, CancellationToken ct = default);
    /// <summary>Conversations the user is an active member of (hub joins conv:{id} groups on connect).</summary>
    Task<IReadOnlyList<Guid>> ListConversationIdsAsync(Guid me, CancellationToken ct = default);
    /// <summary>Active membership check (hub: Typing / JoinConversation).</summary>
    Task<bool> IsMemberAsync(Guid me, Guid id, CancellationToken ct = default);
}

public interface IMessageService
{
    Task<MessageDto> SendAsync(Guid me, Guid conversationId, SendMessageDto dto, CancellationToken ct = default);
    /// <summary>Newest first. limit &lt;= 0 means the configured PageSize (max 100).</summary>
    Task<IReadOnlyList<MessageDto>> GetHistoryAsync(Guid me, Guid conversationId, long? beforeId, int limit, CancellationToken ct = default);
    Task UnsendAsync(Guid me, long messageId, CancellationToken ct = default);
    Task<IReadOnlyList<StickerPackDto>> ListStickersAsync(CancellationToken ct = default);
    /// <summary>Sends a text message that quotes a note (same rules as SendAsync for a text message).</summary>
    Task<MessageDto> SendNoteReplyAsync(Guid me, Guid conversationId, string? content, string noteContent, CancellationToken ct = default);
}

public interface INoteService
{
    /// <summary>The caller's own active note first, then friends' active notes (newest first); blocked users excluded.</summary>
    Task<IReadOnlyList<NoteDto>> ListAsync(Guid me, CancellationToken ct = default);
    /// <summary>Posts a note, replacing any previous one. 1..60 chars. Visible to friends for 24 h.</summary>
    Task<NoteDto> SetAsync(Guid me, string? content, CancellationToken ct = default);
    /// <summary>Removes the caller's note (no-op if there is none).</summary>
    Task DeleteAsync(Guid me, CancellationToken ct = default);
    /// <summary>Replies to a friend's active note: a text message in the direct chat quoting the note.</summary>
    Task<MessageDto> ReplyAsync(Guid me, Guid noteId, string? content, CancellationToken ct = default);
}

public interface IFileStorageService
{
    /// <summary>Validates size (413), type by magic bytes (400), stores the file and an Attachment row.</summary>
    Task<AttachmentDto> SaveImageAsync(Guid me, Stream content, string fileName, string contentType, CancellationToken ct = default);
    /// <summary>Allowed for the uploader, members of a conversation containing the file, or when it is an avatar.</summary>
    Task<AttachmentFile> OpenReadAsync(Guid me, Guid attachmentId, CancellationToken ct = default);
    /// <summary>Removes the attachment row and the stored file (system use, no access check).</summary>
    Task DeleteAsync(Guid attachmentId, CancellationToken ct = default);
}

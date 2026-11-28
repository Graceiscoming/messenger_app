using Messenger.Domain.Enums;

namespace Messenger.Domain.Dtos;

// ---- Auth / users ----
public sealed record RegisterDto(string Username, string DisplayName, string Password);
public sealed record LoginDto(string Username, string Password);
public sealed record UserDto(Guid Id, string Username, string DisplayName, string? StatusMessage, string? AvatarUrl);
public sealed record AuthResultDto(string AccessToken, string RefreshToken, int ExpiresIn, UserDto User);
public sealed record UpdateProfileDto(string DisplayName, string? StatusMessage);

// ---- Friends ----
public sealed record FriendDto(UserDto User, bool IsOnline, Guid? ConversationId);
public sealed record FriendshipDto(Guid Id, UserDto Requester, UserDto Addressee, FriendshipStatus Status);
public sealed record FriendRequestsDto(IReadOnlyList<FriendshipDto> Incoming, IReadOnlyList<FriendshipDto> Outgoing);

// ---- Messages ----
public sealed record SendMessageDto(MessageType Type, string? Content, Guid? AttachmentId, int? StickerId, long? ReplyToMessageId);
public sealed record StickerDto(int Id, string Code, string ImageUrl);
public sealed record StickerPackDto(int Id, string Name, string? ThumbnailUrl, IReadOnlyList<StickerDto> Stickers);
/// <summary>Preview of the replied-to message. Content is null when that message was unsent.</summary>
public sealed record ReplyPreviewDto(long Id, Guid SenderId, string SenderDisplayName, MessageType Type, string? Content, bool IsUnsent);
/// <summary>The note a message replied to (text snapshot). The note's owner is the other member of the direct chat.</summary>
public sealed record NoteReplyDto(string Content);
public sealed record MessageDto(
    long Id, Guid ConversationId, UserDto Sender, MessageType Type,
    string? Content, string? AttachmentUrl, StickerDto? Sticker, ReplyPreviewDto? ReplyTo,
    DateTime CreatedAt, bool IsUnsent, NoteReplyDto? NoteReply = null);

// ---- Notes ----
public sealed record NoteDto(Guid Id, UserDto User, string Content, DateTime CreatedAt, DateTime ExpiresAt);

// ---- Conversations ----
public sealed record ConversationMemberDto(UserDto User, MemberRole Role, long? LastReadMessageId);
public sealed record ConversationDto(
    Guid Id, ConversationType Type, string? Name, string? AvatarUrl,
    IReadOnlyList<ConversationMemberDto> Members, MessageDto? LastMessage, int UnreadCount, bool IsReadOnly);
public sealed record CreateGroupDto(string Name, IReadOnlyList<Guid> MemberIds);
public sealed record UpdateGroupDto(string? Name);
public sealed record ReadReceiptDto(Guid ConversationId, Guid UserId, long MessageId);

// ---- Files ----
public sealed record AttachmentDto(Guid AttachmentId, string Url, int? Width, int? Height);
/// <summary>Result of FileStorageService.OpenReadAsync. The caller owns and must dispose Content.</summary>
public sealed record AttachmentFile(Stream Content, string ContentType, string FileName);

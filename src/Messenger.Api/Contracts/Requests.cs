using Messenger.Domain.Enums;

namespace Messenger.Api.Contracts;

public sealed record RefreshRequest(string RefreshToken);
public sealed record SendFriendRequestRequest(string Username);
public sealed record DirectConversationRequest(Guid FriendId);
public sealed record AddMembersRequest(IReadOnlyList<Guid> UserIds);
public sealed record SetRoleRequest(MemberRole Role);
public sealed record MarkReadRequest(long MessageId);
public sealed record SetNoteRequest(string? Content);
public sealed record NoteReplyRequest(string? Content);

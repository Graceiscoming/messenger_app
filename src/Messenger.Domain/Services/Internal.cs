using System.Text.RegularExpressions;
using Messenger.Domain.Abstractions;
using Messenger.Domain.Dtos;
using Messenger.Domain.Entities;
using Messenger.Domain.Enums;
using Messenger.Domain.Exceptions;

namespace Messenger.Domain.Services;

internal static class Mapper
{
    public static string? FileUrl(Guid? attachmentId)
        => attachmentId is null ? null : $"/api/v1/files/{attachmentId}";

    public static UserDto ToDto(this User u)
        => new(u.Id, u.Username, u.DisplayName, u.StatusMessage, FileUrl(u.AvatarAttachmentId));

    public static FriendshipDto ToDto(this Friendship f)
        => new(f.Id,
            (f.Requester ?? throw new InvalidOperationException("Friendship.Requester not loaded")).ToDto(),
            (f.Addressee ?? throw new InvalidOperationException("Friendship.Addressee not loaded")).ToDto(),
            f.Status);

    public static StickerDto ToDto(this Sticker s) => new(s.Id, s.Code, s.ImagePath);

    public static MessageDto ToDto(this Message m)
    {
        var sender = m.Sender ?? throw new InvalidOperationException("Message.Sender not loaded");
        var unsent = m.IsUnsent;
        ReplyPreviewDto? reply = null;
        if (m.ReplyToMessageId is not null)
        {
            var r = m.ReplyTo;
            reply = r is null
                ? new ReplyPreviewDto(m.ReplyToMessageId.Value, Guid.Empty, "", MessageType.Text, null, false)
                : new ReplyPreviewDto(r.Id, r.SenderId, r.Sender?.DisplayName ?? "", r.Type,
                    r.IsUnsent ? null : r.Content, r.IsUnsent);
        }
        return new MessageDto(
            m.Id, m.ConversationId, sender.ToDto(), m.Type,
            unsent ? null : m.Content,
            unsent ? null : FileUrl(m.AttachmentId),
            unsent ? null : m.Sticker?.ToDto(),
            reply, m.CreatedAt, unsent);
    }
}

internal static partial class Rules
{
    [GeneratedRegex("^[a-zA-Z0-9_]{3,20}$")]
    private static partial Regex UsernameRegex();

    public static void Username(string? username)
    {
        if (string.IsNullOrWhiteSpace(username) || !UsernameRegex().IsMatch(username.Trim()))
            throw new ValidationException("username", "Username must be 3-20 characters (a-z, 0-9, _).");
    }

    public static void Password(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8
            || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new ValidationException("password", "Password must be at least 8 characters with letters and digits.");
    }

    public static void DisplayName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 50)
            throw new ValidationException("displayName", "Display name must be 1-50 characters.");
    }

    public static void GroupName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 50)
            throw new ValidationException("name", "Group name must be 1-50 characters.");
    }
}

/// <summary>Thai text for system messages posted into groups (UC-09).</summary>
internal static class SystemText
{
    public static string GroupCreated(string actor) => $"{actor} สร้างกลุ่ม";
    public static string MembersAdded(string actor, IEnumerable<string> names) => $"{actor} เพิ่ม {string.Join(", ", names)} เข้ากลุ่ม";
    public static string MemberRemoved(string actor, string target) => $"{actor} ลบ {target} ออกจากกลุ่ม";
    public static string MemberLeft(string actor) => $"{actor} ออกจากกลุ่ม";
    public static string Renamed(string actor, string name) => $"{actor} เปลี่ยนชื่อกลุ่มเป็น {name}";
    public static string AvatarChanged(string actor) => $"{actor} เปลี่ยนรูปกลุ่ม";
    public static string RoleChanged(string actor, string target, MemberRole role)
        => role == MemberRole.Admin ? $"{actor} แต่งตั้ง {target} เป็นแอดมิน" : $"{actor} ยกเลิกสิทธิ์แอดมินของ {target}";
    public static string OwnerTransferred(string newOwner) => $"{newOwner} เป็นเจ้าของกลุ่มคนใหม่";
}

/// <summary>Builds personalised ConversationDto values (unread count, read-only flag).</summary>
internal sealed class ConversationDtoBuilder(
    IConversationRepository conversations, IFriendshipRepository friendships, IUserBlockRepository blocks)
{
    public async Task<ConversationDto?> BuildOneAsync(Guid viewerId, Guid conversationId, CancellationToken ct)
    {
        var s = await conversations.GetSummaryAsync(viewerId, conversationId, ct);
        if (s is null) return null;
        return (await BuildManyAsync(viewerId, new[] { s }, ct))[0];
    }

    public async Task<IReadOnlyList<ConversationDto>> BuildManyAsync(
        Guid viewerId, IReadOnlyList<ConversationSummary> summaries, CancellationToken ct)
    {
        HashSet<Guid> friendIds = new();
        HashSet<Guid> blockRelated = new();
        if (summaries.Any(s => s.Conversation.Type == ConversationType.Direct))
        {
            friendIds = (await friendships.ListAcceptedAsync(viewerId, ct))
                .Select(f => f.OtherUserId(viewerId)).ToHashSet();
            blockRelated = (await blocks.ListRelatedUserIdsAsync(viewerId, ct)).ToHashSet();
        }

        var result = new List<ConversationDto>(summaries.Count);
        foreach (var s in summaries)
        {
            var c = s.Conversation;
            var members = c.ActiveMembers
                .Select(m => new ConversationMemberDto(
                    (m.User ?? throw new InvalidOperationException("ConversationMember.User not loaded")).ToDto(),
                    m.Role, m.LastReadMessageId))
                .ToList();
            var readOnly = false;
            if (c.Type == ConversationType.Direct)
            {
                var other = c.OtherMemberId(viewerId);
                readOnly = other is null || !friendIds.Contains(other.Value) || blockRelated.Contains(other.Value);
            }
            result.Add(new ConversationDto(
                c.Id, c.Type, c.Name, Mapper.FileUrl(c.AvatarAttachmentId), members,
                s.LastMessage?.ToDto(), s.UnreadCount, readOnly));
        }
        return result;
    }
}

internal static class DirectConversations
{
    /// <summary>Finds the single Direct conversation of two users (BR-4) or creates it (not yet saved).</summary>
    public static async Task<(Conversation Conversation, bool Created)> EnsureAsync(
        IConversationRepository repo, Guid a, Guid b, DateTime now, CancellationToken ct)
    {
        var key = Conversation.BuildDirectKey(a, b);
        var existing = await repo.FindDirectAsync(key, ct);
        if (existing is not null) return (existing, false);
        var created = Conversation.CreateDirect(a, b, now);
        await repo.AddAsync(created, ct);
        return (created, true);
    }
}

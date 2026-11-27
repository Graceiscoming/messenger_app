using Messenger.Domain.Enums;
using Messenger.Domain.Exceptions;

namespace Messenger.Domain.Entities;

public sealed class Conversation
{
    // EF Core maps the Members navigation through this backing field (convention: _members).
    private readonly List<ConversationMember> _members = new();

    private Conversation() { }

    public Guid Id { get; private set; }
    public ConversationType Type { get; private set; }
    public string? Name { get; private set; }
    public Guid? AvatarAttachmentId { get; private set; }
    /// <summary>"minId_maxId" for Direct conversations, null for groups.</summary>
    public string? DirectKey { get; private set; }
    public Guid? CreatedById { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public long? LastMessageId { get; private set; }
    public DateTime? LastMessageAt { get; private set; }

    /// <summary>All members including those who left (LeftAt != null).</summary>
    public IReadOnlyCollection<ConversationMember> Members => _members;
    public IEnumerable<ConversationMember> ActiveMembers => _members.Where(m => m.IsActive);
    public bool IsGroup => Type == ConversationType.Group;

    public static string BuildDirectKey(Guid a, Guid b)
        => a.CompareTo(b) <= 0 ? $"{a}_{b}" : $"{b}_{a}";

    public static Conversation CreateDirect(Guid a, Guid b, DateTime now)
    {
        var c = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Direct,
            DirectKey = BuildDirectKey(a, b),
            CreatedAt = now,
        };
        c._members.Add(new ConversationMember(c.Id, a, MemberRole.Member, now));
        c._members.Add(new ConversationMember(c.Id, b, MemberRole.Member, now));
        return c;
    }

    public static Conversation CreateGroup(string name, Guid ownerId, IEnumerable<Guid> memberIds, DateTime now)
    {
        var c = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Group,
            Name = name.Trim(),
            CreatedById = ownerId,
            CreatedAt = now,
        };
        c._members.Add(new ConversationMember(c.Id, ownerId, MemberRole.Owner, now));
        foreach (var id in memberIds.Where(i => i != ownerId).Distinct())
            c._members.Add(new ConversationMember(c.Id, id, MemberRole.Member, now));
        return c;
    }

    public ConversationMember? GetMember(Guid userId) => _members.FirstOrDefault(m => m.UserId == userId);
    public bool IsMember(Guid userId) => GetMember(userId) is { IsActive: true };

    /// <summary>The other participant of a Direct conversation.</summary>
    public Guid? OtherMemberId(Guid userId)
        => _members.Where(m => m.UserId != userId).Select(m => (Guid?)m.UserId).FirstOrDefault();

    public void AddMember(Guid userId, MemberRole role, DateTime now)
    {
        var existing = GetMember(userId);
        if (existing is null)
            _members.Add(new ConversationMember(Id, userId, role, now));
        else if (existing.IsActive)
            throw new ConflictException("User is already a member.");
        else
            existing.Rejoin(role, now);
    }

    public void RemoveMember(Guid userId, DateTime now)
    {
        var m = GetMember(userId);
        if (m is not { IsActive: true }) throw new NotFoundException("Member not found.");
        m.Leave(now);
    }

    public void Rename(string name) => Name = name.Trim();
    public void SetAvatar(Guid? attachmentId) => AvatarAttachmentId = attachmentId;

    public void RegisterMessage(long messageId, DateTime at)
    {
        if (LastMessageId is null || messageId > LastMessageId)
        {
            LastMessageId = messageId;
            LastMessageAt = at;
        }
    }
}

using Messenger.Domain.Enums;

namespace Messenger.Domain.Entities;

public sealed class ConversationMember
{
    private ConversationMember() { }

    internal ConversationMember(Guid conversationId, Guid userId, MemberRole role, DateTime now)
    {
        ConversationId = conversationId;
        UserId = userId;
        Role = role;
        JoinedAt = now;
    }

    public Guid ConversationId { get; private set; }
    public Guid UserId { get; private set; }
    public MemberRole Role { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime? LeftAt { get; private set; }
    public long? LastReadMessageId { get; private set; }

    /// <summary>Navigation; repositories must load it for conversation read models.</summary>
    public User? User { get; private set; }

    public bool IsActive => LeftAt is null;
    public bool CanManageMembers() => IsActive && Role >= MemberRole.Admin;

    public void SetRole(MemberRole role) => Role = role;
    public void Leave(DateTime now) => LeftAt = now;

    public void Rejoin(MemberRole role, DateTime now)
    {
        LeftAt = null;
        Role = role;
        JoinedAt = now;
    }

    /// <summary>Advances the read pointer; never moves backwards. Returns true if it changed.</summary>
    public bool MarkRead(long messageId)
    {
        if (LastReadMessageId is not null && LastReadMessageId >= messageId) return false;
        LastReadMessageId = messageId;
        return true;
    }

    public void AttachUser(User user) => User = user;
}

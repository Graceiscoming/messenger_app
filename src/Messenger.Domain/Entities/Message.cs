using Messenger.Domain.Enums;

namespace Messenger.Domain.Entities;

public sealed class Message
{
    private Message() { }

    public long Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid SenderId { get; private set; }
    public MessageType Type { get; private set; }
    public string? Content { get; private set; }
    public Guid? AttachmentId { get; private set; }
    public int? StickerId { get; private set; }
    public long? ReplyToMessageId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UnsentAt { get; private set; }

    /// <summary>Navigations; repositories must load Sender, Sticker and ReplyTo (with its Sender) for reads.</summary>
    public User? Sender { get; private set; }
    public Sticker? Sticker { get; private set; }
    public Message? ReplyTo { get; private set; }

    public bool IsUnsent => UnsentAt is not null;

    public static Message CreateText(Guid conversationId, Guid senderId, string content, long? replyTo, DateTime now) => new()
    {
        ConversationId = conversationId, SenderId = senderId, Type = MessageType.Text,
        Content = content, ReplyToMessageId = replyTo, CreatedAt = now,
    };

    public static Message CreateImage(Guid conversationId, Guid senderId, Guid attachmentId, string? caption, long? replyTo, DateTime now) => new()
    {
        ConversationId = conversationId, SenderId = senderId, Type = MessageType.Image,
        AttachmentId = attachmentId, Content = caption, ReplyToMessageId = replyTo, CreatedAt = now,
    };

    public static Message CreateSticker(Guid conversationId, Guid senderId, int stickerId, long? replyTo, DateTime now) => new()
    {
        ConversationId = conversationId, SenderId = senderId, Type = MessageType.Sticker,
        StickerId = stickerId, ReplyToMessageId = replyTo, CreatedAt = now,
    };

    public static Message CreateSystem(Guid conversationId, Guid actorId, string content, DateTime now) => new()
    {
        ConversationId = conversationId, SenderId = actorId, Type = MessageType.System,
        Content = content, CreatedAt = now,
    };

    public bool CanUnsend(Guid userId, TimeSpan window, DateTime now)
        => Type != MessageType.System && SenderId == userId && !IsUnsent && now - CreatedAt <= window;

    /// <summary>Marks as unsent and clears content, attachment link and sticker (BR-9).</summary>
    public void Unsend(DateTime now)
    {
        UnsentAt = now;
        Content = null;
        AttachmentId = null;
        StickerId = null;
        Sticker = null;
    }

    public void AttachNavigation(User? sender, Sticker? sticker, Message? replyTo)
    {
        Sender = sender;
        Sticker = sticker;
        ReplyTo = replyTo;
    }
}

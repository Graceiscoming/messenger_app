using Messenger.Domain.Abstractions;
using Messenger.Domain.Dtos;
using Messenger.Domain.Entities;
using Messenger.Domain.Enums;
using Messenger.Domain.Exceptions;
using Microsoft.Extensions.Options;
using DomainOptions = Messenger.Domain.Options;

namespace Messenger.Domain.Services;

internal sealed class MessageService(
    IConversationRepository conversations,
    IMessageRepository messages,
    IUserRepository users,
    IStickerRepository stickers,
    IAttachmentRepository attachments,
    IUserBlockRepository blocks,
    IFriendService friends,
    IFileStorageService files,
    IRealtimeNotifier notifier,
    IUnitOfWork uow,
    IClock clock,
    IOptions<DomainOptions.ChatOptions> chat) : IMessageService
{
    private const int MaxPageSize = 100;

    public Task<MessageDto> SendAsync(Guid me, Guid conversationId, SendMessageDto dto, CancellationToken ct = default)
        => SendCoreAsync(me, conversationId, dto, null, ct);

    public Task<MessageDto> SendNoteReplyAsync(Guid me, Guid conversationId, string? content, string noteContent,
        CancellationToken ct = default)
        => SendCoreAsync(me, conversationId, new SendMessageDto(MessageType.Text, content, null, null, null), noteContent, ct);

    private async Task<MessageDto> SendCoreAsync(Guid me, Guid conversationId, SendMessageDto dto, string? noteContent,
        CancellationToken ct)
    {
        var conversation = await conversations.FindByIdWithMembersAsync(conversationId, ct)
                           ?? throw new NotFoundException("Conversation not found.");
        if (!conversation.IsMember(me))
            throw new ForbiddenException("You are not a member of this conversation.");

        if (conversation.Type == ConversationType.Direct)
        {
            // BR-2 / BR-5: only accepted friends without any block; otherwise the room is read-only.
            var other = conversation.OtherMemberId(me)
                        ?? throw new ForbiddenException("This conversation is read-only.");
            if (!await friends.AreFriendsAsync(me, other, ct) || await blocks.ExistsBetweenAsync(me, other, ct))
                throw new ForbiddenException("You can only send messages to friends.");
        }

        var sender = await users.FindByIdAsync(me, ct) ?? throw new NotFoundException("User not found.");
        var now = clock.UtcNow;

        Message? replyTo = null;
        if (dto.ReplyToMessageId is not null)
        {
            replyTo = await messages.FindByIdAsync(dto.ReplyToMessageId.Value, ct);
            if (replyTo is null || replyTo.ConversationId != conversationId)
                throw new ValidationException("replyToMessageId", "Replied-to message not found in this conversation.");
        }

        Message message;
        Sticker? sticker = null;
        switch (dto.Type)
        {
            case MessageType.Text:
                if (dto.AttachmentId is not null || dto.StickerId is not null)
                    throw new ValidationException("type", "Text messages cannot carry an attachment or sticker.");
                var text = ValidateText(dto.Content, chat.Value.MaxMessageLength, required: true)!;
                message = Message.CreateText(conversationId, me, text, dto.ReplyToMessageId, now, noteContent);
                break;

            case MessageType.Image:
                if (dto.AttachmentId is null)
                    throw new ValidationException("attachmentId", "attachmentId is required for image messages.");
                var attachment = await attachments.FindByIdAsync(dto.AttachmentId.Value, ct)
                                 ?? throw new NotFoundException("Attachment not found.");
                if (attachment.UploaderId != me)
                    throw new ForbiddenException("You can only send images you uploaded.");
                if (await attachments.IsReferencedAsync(attachment.Id, ct))
                    throw new ConflictException("This attachment has already been used.");
                var caption = ValidateText(dto.Content, chat.Value.MaxCaptionLength, required: false);
                message = Message.CreateImage(conversationId, me, attachment.Id, caption, dto.ReplyToMessageId, now);
                break;

            case MessageType.Sticker:
                if (dto.StickerId is null)
                    throw new ValidationException("stickerId", "stickerId is required for sticker messages.");
                sticker = await stickers.FindByIdAsync(dto.StickerId.Value, ct)
                          ?? throw new NotFoundException("Sticker not found.");
                message = Message.CreateSticker(conversationId, me, sticker.Id, dto.ReplyToMessageId, now);
                break;

            default:
                throw new ValidationException("type", "Unsupported message type.");
        }

        message.AttachNavigation(sender, sticker, replyTo);
        await uow.ExecuteInTransactionAsync(async token =>
        {
            await messages.AddAsync(message, token);
            await uow.SaveChangesAsync(token);          // assigns message.Id
            conversation.RegisterMessage(message.Id, message.CreatedAt);
            await uow.SaveChangesAsync(token);
        }, ct);

        var result = message.ToDto();
        await notifier.NotifyMessageReceived(conversation.ActiveMembers.Select(m => m.UserId).ToList(), result, ct);
        return result;
    }

    public async Task<IReadOnlyList<MessageDto>> GetHistoryAsync(Guid me, Guid conversationId, long? beforeId, int limit,
        CancellationToken ct = default)
    {
        var conversation = await conversations.FindByIdWithMembersAsync(conversationId, ct)
                           ?? throw new NotFoundException("Conversation not found.");
        if (!conversation.IsMember(me))
            throw new ForbiddenException("You are not a member of this conversation.");

        var size = limit <= 0 ? chat.Value.PageSize : Math.Min(limit, MaxPageSize);
        var page = await messages.GetPageAsync(conversationId, beforeId, size, ct);
        return page.Select(m => m.ToDto()).ToList();
    }

    public async Task UnsendAsync(Guid me, long messageId, CancellationToken ct = default)
    {
        var message = await messages.FindByIdAsync(messageId, ct) ?? throw new NotFoundException("Message not found.");
        if (message.SenderId != me || message.Type == MessageType.System)
            throw new ForbiddenException("You can only unsend your own messages.");   // BR-8
        if (message.IsUnsent)
            throw new ConflictException("Message was already unsent.");

        var conversation = await conversations.FindByIdWithMembersAsync(message.ConversationId, ct)
                           ?? throw new NotFoundException("Conversation not found.");
        if (!conversation.IsMember(me))
            throw new ForbiddenException("You are not a member of this conversation.");

        var now = clock.UtcNow;
        var window = TimeSpan.FromHours(chat.Value.UnsendWindowHours);
        if (!message.CanUnsend(me, window, now))
            throw new ConflictException("The unsend window has passed.");             // BR-8

        var attachmentId = message.AttachmentId;
        message.Unsend(now);                                                          // BR-9
        await uow.SaveChangesAsync(ct);

        if (attachmentId is not null) await files.DeleteAsync(attachmentId.Value, ct); // BR-9
        await notifier.NotifyMessageUnsent(message.ConversationId, message.Id, ct);
    }

    public async Task<IReadOnlyList<StickerPackDto>> ListStickersAsync(CancellationToken ct = default)
    {
        var packs = await stickers.ListPacksAsync(ct);
        return packs.Select(p => new StickerPackDto(p.Id, p.Name, p.ThumbnailPath,
            p.Stickers.Select(s => s.ToDto()).ToList())).ToList();
    }

    /// <summary>BR-10: 1..max chars and not whitespace-only (captions may be empty).</summary>
    private static string? ValidateText(string? content, int max, bool required)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            if (required) throw new ValidationException("content", "Message content is required.");
            return null;
        }
        var trimmed = content.Trim();
        if (trimmed.Length > max)
            throw new ValidationException("content", $"Content must be at most {max} characters.");
        return trimmed;
    }
}

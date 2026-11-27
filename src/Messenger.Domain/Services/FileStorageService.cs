using Messenger.Domain.Abstractions;
using Messenger.Domain.Dtos;
using Messenger.Domain.Entities;
using Messenger.Domain.Exceptions;
using Microsoft.Extensions.Options;
using DomainOptions = Messenger.Domain.Options;

namespace Messenger.Domain.Services;

internal sealed class FileStorageService(
    IAttachmentRepository attachments,
    IMessageRepository messages,
    IConversationRepository conversations,
    IFileStore store,
    IImageInspector inspector,
    IUnitOfWork uow,
    IClock clock,
    IOptions<DomainOptions.StorageOptions> options) : IFileStorageService
{
    public async Task<AttachmentDto> SaveImageAsync(Guid me, Stream content, string fileName, string contentType,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var max = options.Value.MaxImageBytes;

        // Buffer with a hard cap so we never trust Content-Length and can inspect magic bytes (seekable).
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > max)
                throw new PayloadTooLargeException($"Image exceeds the {max} byte limit.");
            buffer.Write(chunk, 0, read);
        }
        if (buffer.Length == 0) throw new ValidationException("file", "File is empty.");

        buffer.Position = 0;
        var info = inspector.Inspect(buffer)
                   ?? throw new ValidationException("file", "Only jpeg, png, webp and gif images are allowed.");
        buffer.Position = 0;

        var storedName = $"{Guid.NewGuid():N}{info.Extension}";
        await store.SaveAsync(buffer, storedName, ct);
        try
        {
            var original = Path.GetFileName(fileName ?? "image");
            if (original.Length > 255) original = original[..255];
            var attachment = Attachment.Create(me, storedName, original, info.ContentType, buffer.Length,
                info.Width, info.Height, clock.UtcNow);
            await attachments.AddAsync(attachment, ct);
            await uow.SaveChangesAsync(ct);
            return new AttachmentDto(attachment.Id, Mapper.FileUrl(attachment.Id)!, attachment.Width, attachment.Height);
        }
        catch
        {
            await store.DeleteAsync(storedName, CancellationToken.None);
            throw;
        }
    }

    public async Task<AttachmentFile> OpenReadAsync(Guid me, Guid attachmentId, CancellationToken ct = default)
    {
        var attachment = await attachments.FindByIdAsync(attachmentId, ct)
                         ?? throw new NotFoundException("File not found.");

        if (!await CanAccessAsync(me, attachment, ct))
            throw new ForbiddenException("You cannot access this file.");

        var stream = await store.OpenReadAsync(attachment.StoredFileName, ct);
        return new AttachmentFile(stream, attachment.ContentType, attachment.OriginalFileName);
    }

    public async Task DeleteAsync(Guid attachmentId, CancellationToken ct = default)
    {
        var attachment = await attachments.FindByIdAsync(attachmentId, ct);
        if (attachment is null) return;
        attachments.Remove(attachment);
        await uow.SaveChangesAsync(ct);
        await store.DeleteAsync(attachment.StoredFileName, ct);
    }

    private async Task<bool> CanAccessAsync(Guid me, Attachment attachment, CancellationToken ct)
    {
        if (attachment.UploaderId == me) return true;
        if (await attachments.IsAvatarAsync(attachment.Id, ct)) return true;
        foreach (var convId in await messages.ListConversationIdsByAttachmentAsync(attachment.Id, ct))
        {
            var conv = await conversations.FindByIdWithMembersAsync(convId, ct);
            if (conv is not null && conv.IsMember(me)) return true;
        }
        return false;
    }
}

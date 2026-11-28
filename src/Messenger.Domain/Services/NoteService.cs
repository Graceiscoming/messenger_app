using Messenger.Domain.Abstractions;
using Messenger.Domain.Dtos;
using Messenger.Domain.Entities;
using Messenger.Domain.Exceptions;

namespace Messenger.Domain.Services;

/// <summary>Instagram-style notes: a 60-char status shown to friends for 24 h; friends can reply into the direct chat.</summary>
internal sealed class NoteService(
    INoteRepository notes,
    IUserRepository users,
    IUserBlockRepository blocks,
    IFriendService friends,
    IConversationService conversations,
    IMessageService messages,
    IRealtimeNotifier notifier,
    IUnitOfWork uow,
    IClock clock) : INoteService
{
    public async Task<IReadOnlyList<NoteDto>> ListAsync(Guid me, CancellationToken ct = default)
    {
        var authors = await VisibleAuthorsAsync(me, ct);
        authors.Add(me);
        var list = await notes.ListActiveByUsersAsync(authors, clock.UtcNow, ct);
        return list
            .OrderByDescending(n => n.UserId == me)
            .ThenByDescending(n => n.CreatedAt)
            .Select(n => n.ToDto())
            .ToList();
    }

    public async Task<NoteDto> SetAsync(Guid me, string? content, CancellationToken ct = default)
    {
        var text = Rules.NoteContent(content);
        var user = await users.FindByIdAsync(me, ct) ?? throw new NotFoundException("User not found.");

        foreach (var old in await notes.ListByUserAsync(me, ct)) notes.Remove(old); // one note per user
        var note = Note.Create(me, text, clock.UtcNow);
        await notes.AddAsync(note, ct);
        await uow.SaveChangesAsync(ct);

        note.AttachUser(user);
        await NotifyAsync(me, ct);
        return note.ToDto();
    }

    public async Task DeleteAsync(Guid me, CancellationToken ct = default)
    {
        var existing = await notes.ListByUserAsync(me, ct);
        if (existing.Count == 0) return;
        foreach (var n in existing) notes.Remove(n);
        await uow.SaveChangesAsync(ct);
        await NotifyAsync(me, ct);
    }

    public async Task<MessageDto> ReplyAsync(Guid me, Guid noteId, string? content, CancellationToken ct = default)
    {
        var note = await notes.FindByIdAsync(noteId, ct);
        if (note is null || !note.IsActive(clock.UtcNow))
            throw new NotFoundException("Note not found or expired.");
        if (note.UserId == me)
            throw new ValidationException("noteId", "You cannot reply to your own note.");
        // Same visibility rule as ListAsync: accepted friends, no block either way (BR-2 / BR-5).
        if (!await friends.AreFriendsAsync(me, note.UserId, ct) || await blocks.ExistsBetweenAsync(me, note.UserId, ct))
            throw new ForbiddenException("You can only reply to your friends' notes.");

        var conversation = await conversations.GetOrCreateDirectAsync(me, note.UserId, ct);
        return await messages.SendNoteReplyAsync(me, conversation.Id, content, note.Content, ct);
    }

    /// <summary>Friends who see the user's note and whose notes the user sees: accepted, not blocked either way.</summary>
    private async Task<HashSet<Guid>> VisibleAuthorsAsync(Guid me, CancellationToken ct)
    {
        var friendIds = await friends.GetFriendIdsAsync(me, ct);
        var blocked = await blocks.ListRelatedUserIdsAsync(me, ct);
        return friendIds.Except(blocked).ToHashSet();
    }

    private async Task NotifyAsync(Guid me, CancellationToken ct)
    {
        var recipients = await VisibleAuthorsAsync(me, ct);
        recipients.Add(me); // the author's other tabs/devices
        await notifier.NotifyNotesChanged(recipients, me, ct);
    }
}

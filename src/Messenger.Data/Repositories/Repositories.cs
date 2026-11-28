using Messenger.Domain.Abstractions;
using Messenger.Domain.Entities;
using Messenger.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Messenger.Data.Repositories;

internal sealed class UserRepository(MessengerDbContext db) : IUserRepository
{
    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> FindByNormalizedUsernameAsync(string normalizedUsername, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.NormalizedUsername == normalizedUsername, ct);

    public async Task<IReadOnlyList<User>> FindByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var list = ids.Distinct().ToList();
        // Tracked on purpose: services attach these users to new ConversationMember rows; no-tracking
        // instances would be marked Added and re-inserted.
        return await db.Users.Where(u => list.Contains(u.Id)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<User>> SearchByUsernamePrefixAsync(string normalizedPrefix, int limit, CancellationToken ct = default)
        => await db.Users.AsNoTracking()
            .Where(u => u.NormalizedUsername.StartsWith(normalizedPrefix))
            .OrderBy(u => u.NormalizedUsername).Take(limit).ToListAsync(ct);

    public async Task AddAsync(User user, CancellationToken ct = default) => await db.Users.AddAsync(user, ct);
}

internal sealed class FriendshipRepository(MessengerDbContext db) : IFriendshipRepository
{
    private IQueryable<Friendship> WithUsers => db.Friendships.Include(f => f.Requester).Include(f => f.Addressee);

    public Task<Friendship?> FindBetweenAsync(Guid userA, Guid userB, CancellationToken ct = default)
        => WithUsers.FirstOrDefaultAsync(f =>
            (f.RequesterId == userA && f.AddresseeId == userB) || (f.RequesterId == userB && f.AddresseeId == userA), ct);

    public Task<Friendship?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => WithUsers.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<IReadOnlyList<Friendship>> ListAcceptedAsync(Guid userId, CancellationToken ct = default)
        => await WithUsers.AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Accepted && (f.RequesterId == userId || f.AddresseeId == userId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Friendship>> ListPendingAsync(Guid userId, CancellationToken ct = default)
        => await WithUsers.AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Pending && (f.RequesterId == userId || f.AddresseeId == userId))
            .OrderByDescending(f => f.CreatedAt).ToListAsync(ct);

    public async Task AddAsync(Friendship friendship, CancellationToken ct = default) => await db.Friendships.AddAsync(friendship, ct);
    public void Remove(Friendship friendship) => db.Friendships.Remove(friendship);
}

internal sealed class UserBlockRepository(MessengerDbContext db) : IUserBlockRepository
{
    public Task<UserBlock?> FindAsync(Guid blockerId, Guid blockedId, CancellationToken ct = default)
        => db.UserBlocks.FirstOrDefaultAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId, ct);

    public Task<bool> ExistsBetweenAsync(Guid userA, Guid userB, CancellationToken ct = default)
        => db.UserBlocks.AsNoTracking().AnyAsync(b =>
            (b.BlockerId == userA && b.BlockedId == userB) || (b.BlockerId == userB && b.BlockedId == userA), ct);

    public async Task<IReadOnlyList<Guid>> ListBlockerIdsAsync(Guid userId, CancellationToken ct = default)
        => await db.UserBlocks.AsNoTracking().Where(b => b.BlockedId == userId).Select(b => b.BlockerId).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> ListRelatedUserIdsAsync(Guid userId, CancellationToken ct = default)
        => await db.UserBlocks.AsNoTracking().Where(b => b.BlockerId == userId).Select(b => b.BlockedId)
            .Union(db.UserBlocks.Where(b => b.BlockedId == userId).Select(b => b.BlockerId))
            .ToListAsync(ct);

    public async Task AddAsync(UserBlock block, CancellationToken ct = default) => await db.UserBlocks.AddAsync(block, ct);
    public void Remove(UserBlock block) => db.UserBlocks.Remove(block);
}

internal sealed class ConversationRepository(MessengerDbContext db) : IConversationRepository
{
    public Task<Conversation?> FindByIdWithMembersAsync(Guid id, CancellationToken ct = default)
        => db.Conversations.Include(c => c.Members).ThenInclude(m => m.User).FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Conversation?> FindDirectAsync(string directKey, CancellationToken ct = default)
        => db.Conversations.Include(c => c.Members).ThenInclude(m => m.User).FirstOrDefaultAsync(c => c.DirectKey == directKey, ct);

    public async Task<IReadOnlyDictionary<string, Guid>> GetDirectConversationIdsAsync(IEnumerable<string> directKeys, CancellationToken ct = default)
    {
        var keys = directKeys.Distinct().ToList();
        var rows = await db.Conversations.AsNoTracking()
            .Where(c => c.DirectKey != null && keys.Contains(c.DirectKey))
            .Select(c => new { Key = c.DirectKey!, c.Id }).ToListAsync(ct);
        return rows.ToDictionary(r => r.Key, r => r.Id);
    }

    public Task<IReadOnlyList<ConversationSummary>> ListForUserAsync(Guid userId, CancellationToken ct = default)
        => LoadSummariesAsync(userId, null, ct);

    public async Task<ConversationSummary?> GetSummaryAsync(Guid userId, Guid conversationId, CancellationToken ct = default)
        => (await LoadSummariesAsync(userId, conversationId, ct)).FirstOrDefault();

    /// <summary>
    /// Constant number of queries (3) regardless of the number of conversations:
    /// 1) ids + last message id + unread count computed in SQL via correlated subqueries,
    /// 2) conversations with members + users, 3) last messages with sender + sticker.
    /// </summary>
    private async Task<IReadOnlyList<ConversationSummary>> LoadSummariesAsync(Guid userId, Guid? onlyId, CancellationToken ct)
    {
        var q = db.Conversations.AsNoTracking()
            .Where(c => c.Members.Any(m => m.UserId == userId && m.LeftAt == null));
        if (onlyId is { } one) q = q.Where(c => c.Id == one);

        var rows = await q.Select(c => new
        {
            c.Id,
            Sort = c.LastMessageAt ?? c.CreatedAt,
            LastId = db.Messages.Where(m => m.ConversationId == c.Id).Max(m => (long?)m.Id),
            Unread = db.Messages.Count(m => m.ConversationId == c.Id && m.SenderId != userId && m.UnsentAt == null
                && m.Id > (c.Members.Where(x => x.UserId == userId).Select(x => x.LastReadMessageId).FirstOrDefault() ?? 0L)),
        }).OrderByDescending(r => r.Sort).ToListAsync(ct);

        if (rows.Count == 0) return [];

        var convIds = rows.Select(r => r.Id).ToList();
        var convs = (await db.Conversations.AsNoTracking().AsSplitQuery()
            .Include(c => c.Members).ThenInclude(m => m.User)
            .Where(c => convIds.Contains(c.Id)).ToListAsync(ct)).ToDictionary(c => c.Id);

        var lastIds = rows.Where(r => r.LastId != null).Select(r => r.LastId!.Value).ToList();
        var msgs = (await db.Messages.AsNoTracking()
            .Include(m => m.Sender).Include(m => m.Sticker)
            .Where(m => lastIds.Contains(m.Id)).ToListAsync(ct)).ToDictionary(m => m.Id);

        return rows.Select(r => new ConversationSummary(
            convs[r.Id],
            r.LastId is { } id && msgs.TryGetValue(id, out var m) ? m : null,
            r.Unread)).ToList();
    }

    public async Task<IReadOnlyList<Guid>> ListConversationIdsForUserAsync(Guid userId, CancellationToken ct = default)
        => await db.ConversationMembers.AsNoTracking()
            .Where(m => m.UserId == userId && m.LeftAt == null).Select(m => m.ConversationId).ToListAsync(ct);

    public Task<bool> IsActiveMemberAsync(Guid conversationId, Guid userId, CancellationToken ct = default)
        => db.ConversationMembers.AsNoTracking()
            .AnyAsync(m => m.ConversationId == conversationId && m.UserId == userId && m.LeftAt == null, ct);

    public async Task AddAsync(Conversation conversation, CancellationToken ct = default) => await db.Conversations.AddAsync(conversation, ct);
}

internal sealed class MessageRepository(MessengerDbContext db) : IMessageRepository
{
    private IQueryable<Message> WithIncludes => db.Messages
        .Include(m => m.Sender).Include(m => m.Sticker)
        .Include(m => m.ReplyTo).ThenInclude(r => r!.Sender);

    public async Task AddAsync(Message message, CancellationToken ct = default) => await db.Messages.AddAsync(message, ct);

    public Task<Message?> FindByIdAsync(long id, CancellationToken ct = default)
        => WithIncludes.FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<IReadOnlyList<Message>> GetPageAsync(Guid conversationId, long? beforeId, int limit, CancellationToken ct = default)
    {
        var q = WithIncludes.AsNoTracking().Where(m => m.ConversationId == conversationId);
        if (beforeId is { } b) q = q.Where(m => m.Id < b);
        return await q.OrderByDescending(m => m.Id).Take(limit).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> ListConversationIdsByAttachmentAsync(Guid attachmentId, CancellationToken ct = default)
        => await db.Messages.AsNoTracking()
            .Where(m => m.AttachmentId == attachmentId && m.UnsentAt == null)
            .Select(m => m.ConversationId).Distinct().ToListAsync(ct);
}

internal sealed class AttachmentRepository(MessengerDbContext db) : IAttachmentRepository
{
    public async Task AddAsync(Attachment attachment, CancellationToken ct = default) => await db.Attachments.AddAsync(attachment, ct);

    public Task<Attachment?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => db.Attachments.FirstOrDefaultAsync(a => a.Id == id, ct);

    public void Remove(Attachment attachment) => db.Attachments.Remove(attachment);

    public async Task<bool> IsAvatarAsync(Guid attachmentId, CancellationToken ct = default)
        => await db.Users.AnyAsync(u => u.AvatarAttachmentId == attachmentId, ct)
           || await db.Conversations.AnyAsync(c => c.AvatarAttachmentId == attachmentId, ct);

    public Task<bool> IsReferencedAsync(Guid attachmentId, CancellationToken ct = default)
        => db.Messages.AsNoTracking().Where(m => m.AttachmentId == attachmentId).Select(m => m.Id).Take(1)
            .Select(_ => true)
            .Concat(db.Users.AsNoTracking().Where(u => u.AvatarAttachmentId == attachmentId).Select(_ => true))
            .Concat(db.Conversations.AsNoTracking().Where(c => c.AvatarAttachmentId == attachmentId).Select(_ => true))
            .AnyAsync(ct);
}

internal sealed class StickerRepository(MessengerDbContext db) : IStickerRepository
{
    public async Task<IReadOnlyList<StickerPack>> ListPacksAsync(CancellationToken ct = default)
        => await db.StickerPacks.AsNoTracking().Include(p => p.Stickers).OrderBy(p => p.Id).ToListAsync(ct);

    public Task<Sticker?> FindByIdAsync(int id, CancellationToken ct = default)
        // Tracked on purpose: the sticker is attached to a new Message; a no-tracking instance would be re-inserted.
        => db.Stickers.FirstOrDefaultAsync(s => s.Id == id, ct);
}

internal sealed class NoteRepository(MessengerDbContext db) : INoteRepository
{
    public Task<Note?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => db.Notes.Include(n => n.User).FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<IReadOnlyList<Note>> ListByUserAsync(Guid userId, CancellationToken ct = default)
        => await db.Notes.Where(n => n.UserId == userId).ToListAsync(ct);

    public async Task<IReadOnlyList<Note>> ListActiveByUsersAsync(IReadOnlyCollection<Guid> userIds, DateTime now,
        CancellationToken ct = default)
        => await db.Notes.AsNoTracking().Include(n => n.User)
            .Where(n => userIds.Contains(n.UserId) && n.ExpiresAt > now)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(Note note, CancellationToken ct = default) => await db.Notes.AddAsync(note, ct);
    public void Remove(Note note) => db.Notes.Remove(note);
}

internal sealed class RefreshTokenRepository(MessengerDbContext db) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token, CancellationToken ct = default) => await db.RefreshTokens.AddAsync(token, ct);

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default)
        => db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
}

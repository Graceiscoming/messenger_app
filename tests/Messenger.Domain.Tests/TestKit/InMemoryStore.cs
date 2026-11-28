using Messenger.Domain.Abstractions;
using Messenger.Domain.Entities;

namespace Messenger.Domain.Tests.TestKit;

/// <summary>In-memory implementation of every repository + unit of work, mimicking what Data must provide.</summary>
public sealed class InMemoryStore : IUnitOfWork
{
    public List<User> Users { get; } = new();
    public List<RefreshToken> RefreshTokens { get; } = new();
    public List<Friendship> Friendships { get; } = new();
    public List<UserBlock> Blocks { get; } = new();
    public List<Conversation> Conversations { get; } = new();
    public List<Message> Messages { get; } = new();
    public List<Attachment> Attachments { get; } = new();
    public List<Sticker> Stickers { get; } = new();
    public List<Note> Notes { get; } = new();
    public int SaveCount { get; private set; }
    private long _nextMessageId = 1;

    public IUserRepository UserRepo { get; }
    public IFriendshipRepository FriendshipRepo { get; }
    public IUserBlockRepository BlockRepo { get; }
    public IConversationRepository ConversationRepo { get; }
    public IMessageRepository MessageRepo { get; }
    public IAttachmentRepository AttachmentRepo { get; }
    public IStickerRepository StickerRepo { get; }
    public IRefreshTokenRepository RefreshTokenRepo { get; }
    public INoteRepository NoteRepo { get; }

    public InMemoryStore()
    {
        UserRepo = new UserRepo(this);
        FriendshipRepo = new FriendshipRepo(this);
        BlockRepo = new BlockRepo(this);
        ConversationRepo = new ConversationRepo(this);
        MessageRepo = new MessageRepo(this);
        AttachmentRepo = new AttachmentRepo(this);
        StickerRepo = new StickerRepo(this);
        RefreshTokenRepo = new RefreshTokenRepo(this);
        NoteRepo = new NoteRepo(this);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(++SaveCount);

    public int TransactionCount { get; private set; }
    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default)
    {
        TransactionCount++;
        return work(ct);
    }

    internal long NextMessageId() => _nextMessageId++;

    internal void Hydrate(Message m)
    {
        var sender = Users.FirstOrDefault(u => u.Id == m.SenderId);
        var sticker = m.StickerId is null ? null : Stickers.FirstOrDefault(s => s.Id == m.StickerId);
        var reply = m.ReplyToMessageId is null ? null : Messages.FirstOrDefault(x => x.Id == m.ReplyToMessageId);
        if (reply is not null) reply.AttachNavigation(Users.FirstOrDefault(u => u.Id == reply.SenderId), null, null);
        m.AttachNavigation(sender, sticker, reply);
    }

    internal void Hydrate(Conversation c)
    {
        foreach (var m in c.Members) m.AttachUser(Users.First(u => u.Id == m.UserId));
    }

    internal void Hydrate(Friendship f)
        => f.AttachUsers(Users.First(u => u.Id == f.RequesterId), Users.First(u => u.Id == f.AddresseeId));
}

internal sealed class UserRepo(InMemoryStore s) : IUserRepository
{
    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(s.Users.FirstOrDefault(u => u.Id == id));
    public Task<User?> FindByNormalizedUsernameAsync(string n, CancellationToken ct = default) => Task.FromResult(s.Users.FirstOrDefault(u => u.NormalizedUsername == n));
    public Task<IReadOnlyList<User>> FindByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<User>>(s.Users.Where(u => ids.Contains(u.Id)).ToList());
    public Task<IReadOnlyList<User>> SearchByUsernamePrefixAsync(string p, int limit, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<User>>(s.Users.Where(u => u.NormalizedUsername.StartsWith(p)).OrderBy(u => u.Username).Take(limit).ToList());
    public Task AddAsync(User user, CancellationToken ct = default) { s.Users.Add(user); return Task.CompletedTask; }
}

internal sealed class FriendshipRepo(InMemoryStore s) : IFriendshipRepository
{
    private Friendship? H(Friendship? f) { if (f is not null) s.Hydrate(f); return f; }
    public Task<Friendship?> FindBetweenAsync(Guid a, Guid b, CancellationToken ct = default)
        => Task.FromResult(H(s.Friendships.FirstOrDefault(f => f.Involves(a) && f.Involves(b))));
    public Task<Friendship?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(H(s.Friendships.FirstOrDefault(f => f.Id == id)));
    public Task<IReadOnlyList<Friendship>> ListAcceptedAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Friendship>>(s.Friendships.Where(f => f.Involves(userId) && f.Status == Domain.Enums.FriendshipStatus.Accepted).Select(f => H(f)!).ToList());
    public Task<IReadOnlyList<Friendship>> ListPendingAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Friendship>>(s.Friendships.Where(f => f.Involves(userId) && f.Status == Domain.Enums.FriendshipStatus.Pending).Select(f => H(f)!).ToList());
    public Task AddAsync(Friendship f, CancellationToken ct = default) { s.Friendships.Add(f); return Task.CompletedTask; }
    public void Remove(Friendship f) => s.Friendships.Remove(f);
}

internal sealed class BlockRepo(InMemoryStore s) : IUserBlockRepository
{
    public Task<UserBlock?> FindAsync(Guid blocker, Guid blocked, CancellationToken ct = default)
        => Task.FromResult(s.Blocks.FirstOrDefault(b => b.BlockerId == blocker && b.BlockedId == blocked));
    public Task<bool> ExistsBetweenAsync(Guid a, Guid b, CancellationToken ct = default)
        => Task.FromResult(s.Blocks.Any(x => (x.BlockerId == a && x.BlockedId == b) || (x.BlockerId == b && x.BlockedId == a)));
    public Task<IReadOnlyList<Guid>> ListBlockerIdsAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Guid>>(s.Blocks.Where(b => b.BlockedId == userId).Select(b => b.BlockerId).ToList());
    public Task<IReadOnlyList<Guid>> ListRelatedUserIdsAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Guid>>(s.Blocks.Where(b => b.BlockerId == userId || b.BlockedId == userId)
            .Select(b => b.BlockerId == userId ? b.BlockedId : b.BlockerId).ToList());
    public Task AddAsync(UserBlock b, CancellationToken ct = default) { s.Blocks.Add(b); return Task.CompletedTask; }
    public void Remove(UserBlock b) => s.Blocks.Remove(b);
}

internal sealed class ConversationRepo(InMemoryStore s) : IConversationRepository
{
    private Conversation? H(Conversation? c) { if (c is not null) s.Hydrate(c); return c; }

    public Task<Conversation?> FindByIdWithMembersAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(H(s.Conversations.FirstOrDefault(c => c.Id == id)));
    public Task<Conversation?> FindDirectAsync(string key, CancellationToken ct = default)
        => Task.FromResult(H(s.Conversations.FirstOrDefault(c => c.DirectKey == key)));
    public Task<IReadOnlyDictionary<string, Guid>> GetDirectConversationIdsAsync(IEnumerable<string> keys, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<string, Guid>>(
            s.Conversations.Where(c => c.DirectKey != null && keys.Contains(c.DirectKey)).ToDictionary(c => c.DirectKey!, c => c.Id));

    private ConversationSummary? Summarize(Guid userId, Conversation c)
    {
        var member = c.GetMember(userId);
        if (member is not { IsActive: true }) return null;
        s.Hydrate(c);
        var msgs = s.Messages.Where(m => m.ConversationId == c.Id).ToList();
        var last = msgs.OrderByDescending(m => m.Id).FirstOrDefault();
        if (last is not null) s.Hydrate(last);
        var unread = msgs.Count(m => m.Id > (member.LastReadMessageId ?? 0) && m.SenderId != userId && !m.IsUnsent);
        return new ConversationSummary(c, last, unread);
    }

    public Task<IReadOnlyList<ConversationSummary>> ListForUserAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ConversationSummary>>(
            s.Conversations.Select(c => Summarize(userId, c)).Where(x => x is not null).Select(x => x!)
                .OrderByDescending(x => x.Conversation.LastMessageAt ?? x.Conversation.CreatedAt).ToList());
    public Task<ConversationSummary?> GetSummaryAsync(Guid userId, Guid id, CancellationToken ct = default)
        => Task.FromResult(s.Conversations.Where(c => c.Id == id).Select(c => Summarize(userId, c)).FirstOrDefault());
    public Task<bool> IsActiveMemberAsync(Guid conversationId, Guid userId, CancellationToken ct = default)
        => Task.FromResult(s.Conversations.Any(c => c.Id == conversationId && c.IsMember(userId)));
    public Task<IReadOnlyList<Guid>> ListConversationIdsForUserAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Guid>>(s.Conversations.Where(c => c.IsMember(userId)).Select(c => c.Id).ToList());
    public Task AddAsync(Conversation c, CancellationToken ct = default) { s.Conversations.Add(c); return Task.CompletedTask; }
}

internal sealed class MessageRepo(InMemoryStore s) : IMessageRepository
{
    public Task AddAsync(Message m, CancellationToken ct = default)
    {
        typeof(Message).GetProperty(nameof(Message.Id))!.SetValue(m, s.NextMessageId());
        s.Messages.Add(m);
        return Task.CompletedTask;
    }
    public Task<Message?> FindByIdAsync(long id, CancellationToken ct = default)
    {
        var m = s.Messages.FirstOrDefault(x => x.Id == id);
        if (m is not null) s.Hydrate(m);
        return Task.FromResult(m);
    }
    public Task<IReadOnlyList<Message>> GetPageAsync(Guid convId, long? beforeId, int limit, CancellationToken ct = default)
    {
        var page = s.Messages.Where(m => m.ConversationId == convId && (beforeId is null || m.Id < beforeId))
            .OrderByDescending(m => m.Id).Take(limit).ToList();
        page.ForEach(s.Hydrate);
        return Task.FromResult<IReadOnlyList<Message>>(page);
    }
    public Task<IReadOnlyList<Guid>> ListConversationIdsByAttachmentAsync(Guid attachmentId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Guid>>(s.Messages.Where(m => m.AttachmentId == attachmentId).Select(m => m.ConversationId).Distinct().ToList());
}

internal sealed class AttachmentRepo(InMemoryStore s) : IAttachmentRepository
{
    public Task AddAsync(Attachment a, CancellationToken ct = default) { s.Attachments.Add(a); return Task.CompletedTask; }
    public Task<Attachment?> FindByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(s.Attachments.FirstOrDefault(a => a.Id == id));
    public void Remove(Attachment a) => s.Attachments.Remove(a);
    public Task<bool> IsAvatarAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(s.Users.Any(u => u.AvatarAttachmentId == id) || s.Conversations.Any(c => c.AvatarAttachmentId == id));
    public Task<bool> IsReferencedAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(s.Messages.Any(m => m.AttachmentId == id)
                           || s.Users.Any(u => u.AvatarAttachmentId == id) || s.Conversations.Any(c => c.AvatarAttachmentId == id));
}

internal sealed class StickerRepo(InMemoryStore s) : IStickerRepository
{
    public Task<IReadOnlyList<StickerPack>> ListPacksAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<StickerPack>>(new List<StickerPack>());
    public Task<Sticker?> FindByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(s.Stickers.FirstOrDefault(x => x.Id == id));
}

internal sealed class NoteRepo(InMemoryStore s) : INoteRepository
{
    private Note Hydrate(Note n) { n.AttachUser(s.Users.First(u => u.Id == n.UserId)); return n; }
    public Task<Note?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(s.Notes.Where(n => n.Id == id).Select(Hydrate).FirstOrDefault());
    public Task<IReadOnlyList<Note>> ListByUserAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Note>>(s.Notes.Where(n => n.UserId == userId).ToList());
    public Task<IReadOnlyList<Note>> ListActiveByUsersAsync(IReadOnlyCollection<Guid> ids, DateTime now, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Note>>(s.Notes.Where(n => ids.Contains(n.UserId) && n.ExpiresAt > now)
            .OrderByDescending(n => n.CreatedAt).Select(Hydrate).ToList());
    public Task AddAsync(Note note, CancellationToken ct = default) { s.Notes.Add(note); return Task.CompletedTask; }
    public void Remove(Note note) => s.Notes.Remove(note);
}

internal sealed class RefreshTokenRepo(InMemoryStore s) : IRefreshTokenRepository
{
    public Task AddAsync(RefreshToken t, CancellationToken ct = default) { s.RefreshTokens.Add(t); return Task.CompletedTask; }
    public Task<RefreshToken?> FindByHashAsync(string h, CancellationToken ct = default) => Task.FromResult(s.RefreshTokens.FirstOrDefault(t => t.TokenHash == h));
}

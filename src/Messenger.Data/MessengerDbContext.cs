using Messenger.Domain.Abstractions;
using Messenger.Domain.Entities;
using Messenger.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Messenger.Data;

public sealed class MessengerDbContext(DbContextOptions<MessengerDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMember> ConversationMembers => Set<ConversationMember>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<StickerPack> StickerPacks => Set<StickerPack>();
    public DbSet<Sticker> Stickers => Set<Sticker>();
    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(MessengerDbContext).Assembly);

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new ConflictException($"Duplicate value violates unique constraint '{pg.ConstraintName}'.");
        }
    }

    // NOTE: EnableRetryOnFailure is deliberately NOT configured. The work delegate mutates tracked entities and is
    // not safe to re-run, so the execution strategy here is the non-retrying default; it is still required
    // by EF for user-initiated transactions when a retrying strategy is ever configured.
    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default)
    {
        if (Database.CurrentTransaction is not null)
        {
            await work(ct);
            return;
        }

        var strategy = Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await Database.BeginTransactionAsync(ct);
            try
            {
                await work(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        });
    }
}

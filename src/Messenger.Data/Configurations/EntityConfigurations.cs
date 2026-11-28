using Messenger.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messenger.Data.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Username).HasMaxLength(32).IsRequired();
        b.Property(x => x.NormalizedUsername).HasMaxLength(32).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(64).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
        b.Property(x => x.StatusMessage).HasMaxLength(140);
        // UNIQUE (lower(username)) from docs/05 is satisfied by a unique index on the stored lower-cased column.
        b.HasIndex(x => x.NormalizedUsername).IsUnique();
        b.HasOne<Attachment>().WithMany().HasForeignKey(x => x.AvatarAttachmentId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.UserId);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> b)
    {
        b.ToTable("friendships", t => t.HasCheckConstraint("ck_friendships_not_self", "requester_id <> addressee_id"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Status).HasConversion<short>();
        b.HasIndex(x => new { x.AddresseeId, x.Status });
        b.HasIndex(x => x.RequesterId);
        b.HasOne(x => x.Requester).WithMany().HasForeignKey(x => x.RequesterId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Addressee).WithMany().HasForeignKey(x => x.AddresseeId).OnDelete(DeleteBehavior.Cascade);
        // The unique (LEAST, GREATEST) pair index cannot be expressed in the fluent API; it is created
        // with raw SQL in the Initial migration (ux_friendships_pair).
    }
}

internal sealed class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
{
    public void Configure(EntityTypeBuilder<UserBlock> b)
    {
        b.ToTable("user_blocks");
        b.HasKey(x => new { x.BlockerId, x.BlockedId });
        b.HasIndex(x => x.BlockedId);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.BlockerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.BlockedId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> b)
    {
        b.ToTable("conversations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Type).HasConversion<short>();
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.DirectKey).HasMaxLength(80);
        b.HasIndex(x => x.DirectKey).IsUnique(); // nullable: Postgres allows many NULLs
        b.Ignore(x => x.ActiveMembers);
        b.Ignore(x => x.IsGroup);
        b.HasOne<Attachment>().WithMany().HasForeignKey(x => x.AvatarAttachmentId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(x => x.Members).WithOne().HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ConversationMemberConfiguration : IEntityTypeConfiguration<ConversationMember>
{
    public void Configure(EntityTypeBuilder<ConversationMember> b)
    {
        b.ToTable("conversation_members");
        b.HasKey(x => new { x.ConversationId, x.UserId });
        b.Property(x => x.Role).HasConversion<short>();
        b.Ignore(x => x.IsActive);
        b.HasIndex(x => x.UserId);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> b)
    {
        // Check relaxed vs docs/05 (unsend nulls content); see docs/decisions.md.
        b.ToTable("messages", t => t.HasCheckConstraint("ck_messages_text_has_content",
            "type <> 0 OR content IS NOT NULL OR unsent_at IS NOT NULL"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd().UseIdentityByDefaultColumn();
        b.Property(x => x.Type).HasConversion<short>();
        b.Property(x => x.Content).HasMaxLength(4000);
        b.Property(x => x.NoteReplyContent).HasMaxLength(Note.MaxLength);
        b.Ignore(x => x.IsUnsent);
        b.HasIndex(x => new { x.ConversationId, x.Id }).IsDescending(false, true);
        b.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Attachment>().WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Sticker).WithMany().HasForeignKey(x => x.StickerId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.ReplyTo).WithMany().HasForeignKey(x => x.ReplyToMessageId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.AttachmentId);
        b.HasIndex(x => x.ReplyToMessageId);
    }
}

internal sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> b)
    {
        b.ToTable("notes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Content).HasMaxLength(Note.MaxLength).IsRequired();
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.ExpiresAt);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> b)
    {
        b.ToTable("attachments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.StoredFileName).HasMaxLength(260).IsRequired();
        b.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UploaderId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StickerPackConfiguration : IEntityTypeConfiguration<StickerPack>
{
    public void Configure(EntityTypeBuilder<StickerPack> b)
    {
        b.ToTable("sticker_packs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(64).IsRequired();
        b.Property(x => x.ThumbnailPath).HasMaxLength(260);
        b.HasMany(x => x.Stickers).WithOne().HasForeignKey(s => s.PackId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Stickers).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasData(StickerSeed.Packs);
    }
}

internal sealed class StickerConfiguration : IEntityTypeConfiguration<Sticker>
{
    public void Configure(EntityTypeBuilder<Sticker> b)
    {
        b.ToTable("stickers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Code).HasMaxLength(64).IsRequired();
        b.Property(x => x.ImagePath).HasMaxLength(260).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasData(StickerSeed.Stickers);
    }
}

using Messenger.Domain.Dtos;
using Messenger.Domain.Entities;
using Messenger.Domain.Enums;
using Messenger.Domain.Exceptions;
using Messenger.Domain.Tests.TestKit;
using NSubstitute;

namespace Messenger.Domain.Tests;

public class UserServiceTests
{
    private readonly World w = new();

    [Fact]
    public async Task Block_FriendRemovesFriendshipAndKeepsRoomReadOnly_UC10()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob");
        var conv = w.MakeFriends(a, b);

        await w.Users.BlockAsync(a.Id, b.Id);

        Assert.Empty(w.Db.Friendships);
        Assert.Single(w.Db.Blocks);
        await w.Notifier.Received().NotifyFriendRemoved(b.Id, a.Id, Arg.Any<CancellationToken>());
        await Assert.ThrowsAsync<ForbiddenException>(() => w.SendText(b, conv.Id, "hi"));
    }

    [Fact]
    public async Task Block_Self_ThrowsValidation()
    {
        var a = w.AddUser("alice");
        await Assert.ThrowsAsync<ValidationException>(() => w.Users.BlockAsync(a.Id, a.Id));
    }

    [Fact]
    public async Task Block_IsIdempotent_UnblockRemoves()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob");
        await w.Users.BlockAsync(a.Id, b.Id);
        await w.Users.BlockAsync(a.Id, b.Id);
        Assert.Single(w.Db.Blocks);
        await w.Users.UnblockAsync(a.Id, b.Id);
        Assert.Empty(w.Db.Blocks);
    }

    [Fact]
    public async Task Search_ExcludesSelfAndUsersWhoBlockedMe_PrefixMatch()
    {
        var a = w.AddUser("alice"); var a2 = w.AddUser("alina"); var a3 = w.AddUser("alan"); w.AddUser("bob");
        w.Block(a3, a);
        var r = await w.Users.SearchAsync(a.Id, "AL");
        Assert.Equal(new[] { "alina" }, r.Select(u => u.Username));
    }

    [Fact]
    public async Task UpdateProfile_Validates_AndSaves()
    {
        var a = w.AddUser("alice");
        var dto = await w.Users.UpdateProfileAsync(a.Id, new UpdateProfileDto("Alice A", "busy"));
        Assert.Equal("busy", dto.StatusMessage);
        await Assert.ThrowsAsync<ValidationException>(() => w.Users.UpdateProfileAsync(a.Id, new UpdateProfileDto("", null)));
        await Assert.ThrowsAsync<ValidationException>(() => w.Users.UpdateProfileAsync(a.Id, new UpdateProfileDto(new string('x', 51), null)));
    }

    [Fact]
    public async Task SetAvatar_StoresImageAndExposesUrl_ReplacesOld()
    {
        var a = w.AddUser("alice");
        var png = FakeImageInspector.PngHeader.Concat(new byte[10]).ToArray();
        var u1 = await w.Users.SetAvatarAsync(a.Id, new MemoryStream(png), "a.png", "image/png");
        var u2 = await w.Users.SetAvatarAsync(a.Id, new MemoryStream(png), "b.png", "image/png");
        Assert.NotNull(u1.AvatarUrl);
        Assert.NotEqual(u1.AvatarUrl, u2.AvatarUrl);
        Assert.Single(w.Db.Attachments);
    }
}

public class FileStorageServiceTests
{
    private readonly World w = new();

    [Fact]
    public async Task SaveImage_OverLimit_ThrowsPayloadTooLarge()
    {
        var a = w.AddUser("alice");
        w.Storage.MaxImageBytes = 100;
        var bytes = FakeImageInspector.PngHeader.Concat(new byte[200]).ToArray();
        await Assert.ThrowsAsync<PayloadTooLargeException>(() => w.Files.SaveImageAsync(a.Id, new MemoryStream(bytes), "a.png", "image/png"));
        Assert.Empty(w.FileStore.Files);
    }

    [Fact]
    public async Task SaveImage_NotAnImageByMagicBytes_ThrowsValidation_EvenIfNamedPng()
    {
        var a = w.AddUser("alice");
        await Assert.ThrowsAsync<ValidationException>(() => w.Files.SaveImageAsync(a.Id, new MemoryStream(new byte[50]), "evil.png", "image/png"));
    }

    [Fact]
    public async Task SaveImage_Empty_ThrowsValidation()
    {
        var a = w.AddUser("alice");
        await Assert.ThrowsAsync<ValidationException>(() => w.Files.SaveImageAsync(a.Id, new MemoryStream(), "a.png", "image/png"));
    }

    [Fact]
    public async Task OpenRead_UploaderMemberAndStranger()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob"); var c = w.AddUser("carol");
        var conv = w.MakeFriends(a, b);
        var png = FakeImageInspector.PngHeader.Concat(new byte[10]).ToArray();
        var att = await w.Files.SaveImageAsync(a.Id, new MemoryStream(png), "a.png", "image/png");

        (await w.Files.OpenReadAsync(a.Id, att.AttachmentId)).Content.Dispose();
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Files.OpenReadAsync(b.Id, att.AttachmentId)); // not sent yet

        await w.Messages.SendAsync(a.Id, conv.Id, new SendMessageDto(MessageType.Image, null, att.AttachmentId, null, null));
        (await w.Files.OpenReadAsync(b.Id, att.AttachmentId)).Content.Dispose();
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Files.OpenReadAsync(c.Id, att.AttachmentId));
        await Assert.ThrowsAsync<NotFoundException>(() => w.Files.OpenReadAsync(a.Id, Guid.NewGuid()));
    }
}

public class EntityTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RefreshToken_IsActive_FalseWhenExpiredOrRevoked()
    {
        var t = RefreshToken.Create(Guid.NewGuid(), "h", T0, TimeSpan.FromDays(7));
        Assert.True(t.IsActive(T0.AddDays(6)));
        Assert.False(t.IsActive(T0.AddDays(8)));
        t.Revoke(T0);
        Assert.False(t.IsActive(T0));
    }

    [Fact]
    public void Conversation_DirectKey_IsSymmetric_BR4()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        Assert.Equal(Conversation.BuildDirectKey(a, b), Conversation.BuildDirectKey(b, a));
    }

    [Fact]
    public void Friendship_Accept_OnlyFromPending_AndInvolves()
    {
        var a = User.Create("alice", "A", "h", T0); var b = User.Create("bob", "B", "h", T0);
        var f = Friendship.Create(a, b, T0);
        f.Accept(T0);
        Assert.Throws<ConflictException>(() => f.Accept(T0));
        Assert.True(f.Involves(a.Id));
        Assert.False(f.Involves(Guid.NewGuid()));
    }

    [Fact]
    public void ConversationMember_CanManageMembers_OnlyAdminOrOwner_BR7()
    {
        var o = Guid.NewGuid(); var m = Guid.NewGuid();
        var c = Conversation.CreateGroup("g", o, new[] { m }, T0);
        Assert.True(c.GetMember(o)!.CanManageMembers());
        Assert.False(c.GetMember(m)!.CanManageMembers());
        c.GetMember(m)!.SetRole(MemberRole.Admin);
        Assert.True(c.GetMember(m)!.CanManageMembers());
        c.RemoveMember(m, T0);
        Assert.False(c.GetMember(m)!.CanManageMembers());
    }

    [Fact]
    public void Message_CanUnsend_RulesOfBR8()
    {
        var s = Guid.NewGuid();
        var m = Message.CreateText(Guid.NewGuid(), s, "x", null, T0);
        var window = TimeSpan.FromHours(24);
        Assert.True(m.CanUnsend(s, window, T0.AddHours(23)));
        Assert.False(m.CanUnsend(Guid.NewGuid(), window, T0));
        Assert.False(m.CanUnsend(s, window, T0.AddHours(25)));
        m.Unsend(T0);
        Assert.False(m.CanUnsend(s, window, T0));
        Assert.Null(m.Content);
        Assert.True(m.IsUnsent);
    }

    [Fact]
    public void Message_System_CannotBeUnsent()
    {
        var s = Guid.NewGuid();
        Assert.False(Message.CreateSystem(Guid.NewGuid(), s, "x", T0).CanUnsend(s, TimeSpan.FromHours(24), T0));
    }

    [Fact]
    public void User_Lockout_AfterMaxFailures()
    {
        var u = User.Create("alice", "A", "h", T0);
        for (var i = 0; i < 5; i++) u.RegisterFailedLogin(T0, 5, TimeSpan.FromMinutes(5));
        Assert.True(u.IsLockedOut(T0.AddMinutes(4)));
        Assert.False(u.IsLockedOut(T0.AddMinutes(6)));
    }

    [Fact]
    public void User_Normalize_LowerCases() => Assert.Equal("alice_1", User.Normalize(" Alice_1 "));

public class AvatarSingleUseTests
{
    [Fact]
    public async Task SetAvatar_OldAvatarKeptWhenStillReferencedElsewhere()
    {
        var w = new World();
        var a = w.AddUser("alice");
        var png = FakeImageInspector.PngHeader.Concat(new byte[10]).ToArray();
        await w.Users.SetAvatarAsync(a.Id, new MemoryStream(png), "a.png", "image/png");
        var oldId = w.Db.Users.Single().AvatarAttachmentId!.Value;
        // Simulate another reference (conversation avatar) to the old attachment.
        var conv = Domain.Entities.Conversation.CreateGroup("G", a.Id, new[] { Guid.NewGuid() }, w.Clock.UtcNow);
        conv.SetAvatar(oldId);
        w.Db.Conversations.Add(conv);

        await w.Users.SetAvatarAsync(a.Id, new MemoryStream(png), "b.png", "image/png");
        Assert.Contains(w.Db.Attachments, x => x.Id == oldId);
    }
}
}

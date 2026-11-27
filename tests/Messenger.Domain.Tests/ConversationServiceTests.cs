using Messenger.Domain.Dtos;
using Messenger.Domain.Entities;
using Messenger.Domain.Enums;
using Messenger.Domain.Exceptions;
using Messenger.Domain.Tests.TestKit;
using NSubstitute;

namespace Messenger.Domain.Tests;

public class ConversationServiceTests
{
    private readonly World w = new();
    private readonly User owner, b, c, d;

    public ConversationServiceTests()
    {
        owner = w.AddUser("owner"); b = w.AddUser("bob"); c = w.AddUser("carol"); d = w.AddUser("dave");
        w.MakeFriends(owner, b); w.MakeFriends(owner, c); w.MakeFriends(owner, d);
        w.MakeFriends(b, c);
    }

    private Task<ConversationDto> NewGroup(params User[] members)
        => w.Conversations.CreateGroupAsync(owner.Id, new CreateGroupDto("Team", members.Select(m => m.Id).ToList()));

    // ---- direct
    [Fact]
    public async Task GetOrCreateDirect_Friend_ReturnsSameRoomEachTime_BR4()
    {
        var x = await w.Conversations.GetOrCreateDirectAsync(owner.Id, b.Id);
        var y = await w.Conversations.GetOrCreateDirectAsync(b.Id, owner.Id);
        Assert.Equal(x.Id, y.Id);
        Assert.Equal(1, w.Db.Conversations.Count(cv => cv.Type == ConversationType.Direct && cv.IsMember(owner.Id) && cv.IsMember(b.Id)));
    }

    [Fact]
    public async Task GetOrCreateDirect_NonFriend_ThrowsForbidden()
    {
        var e = w.AddUser("eve");
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Conversations.GetOrCreateDirectAsync(owner.Id, e.Id));
    }

    // ---- create group
    [Fact]
    public async Task CreateGroup_CreatorIsOwner_AddsMembersAndSystemMessage_BR6()
    {
        var dto = await NewGroup(b, c);
        Assert.Equal(ConversationType.Group, dto.Type);
        Assert.Equal(MemberRole.Owner, dto.Members.Single(m => m.User.Id == owner.Id).Role);
        Assert.Equal(3, dto.Members.Count);
        var sys = w.Db.Messages.Single();
        Assert.Equal(MessageType.System, sys.Type);
        Assert.Equal(sys.Id, w.Db.Conversations.Last().LastMessageId);
        await w.Notifier.Received().NotifyMembershipChanged(dto.Id, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateGroup_WithNoOtherMember_ThrowsValidation_BR6()
        => await Assert.ThrowsAsync<ValidationException>(() => NewGroup());

    [Fact]
    public async Task CreateGroup_MemberNotFriend_ThrowsForbidden_BR6()
    {
        var e = w.AddUser("eve");
        await Assert.ThrowsAsync<ForbiddenException>(() => NewGroup(b, e));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateGroup_BlankName_ThrowsValidation(string name)
        => await Assert.ThrowsAsync<ValidationException>(() => w.Conversations.CreateGroupAsync(owner.Id, new CreateGroupDto(name, new[] { b.Id })));

    [Fact]
    public async Task CreateGroup_NameOver50_ThrowsValidation()
        => await Assert.ThrowsAsync<ValidationException>(() => w.Conversations.CreateGroupAsync(owner.Id, new CreateGroupDto(new string('n', 51), new[] { b.Id })));

    // ---- add members
    [Fact]
    public async Task AddMembers_ByOwner_AddsFriendsAndPostsSystemMessage_BR6_BR7()
    {
        var g = await NewGroup(b);
        var dto = await w.Conversations.AddMembersAsync(owner.Id, g.Id, new[] { c.Id });
        Assert.Equal(3, dto.Members.Count);
        Assert.Equal(2, w.Db.Messages.Count);
    }

    [Fact]
    public async Task AddMembers_NonFriendOfAdder_ThrowsForbidden_BR6()
    {
        var g = await NewGroup(b);
        var e = w.AddUser("eve");
        w.MakeFriends(b, e);   // friend of bob, but not of the owner
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Conversations.AddMembersAsync(owner.Id, g.Id, new[] { e.Id }));
    }

    [Fact]
    public async Task AddMembers_ByPlainMember_ThrowsForbidden_BR7()
    {
        var g = await NewGroup(b, c);
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Conversations.AddMembersAsync(b.Id, g.Id, new[] { d.Id }));
    }

    [Fact]
    public async Task AddMembers_ByAdmin_Allowed_ButOnlyHisFriends_BR7()
    {
        var g = await NewGroup(b, c);
        await w.Conversations.SetRoleAsync(owner.Id, g.Id, b.Id, MemberRole.Admin);
        var e = w.AddUser("eve"); w.MakeFriends(b, e);
        var dto = await w.Conversations.AddMembersAsync(b.Id, g.Id, new[] { e.Id });
        Assert.Contains(dto.Members, m => m.User.Id == e.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Conversations.AddMembersAsync(b.Id, g.Id, new[] { d.Id }));
    }

    [Fact]
    public async Task AddMembers_AlreadyMember_ThrowsConflict()
    {
        var g = await NewGroup(b);
        await Assert.ThrowsAsync<ConflictException>(() => w.Conversations.AddMembersAsync(owner.Id, g.Id, new[] { b.Id }));
    }

    [Fact]
    public async Task AddMembers_RejoinsFormerMember()
    {
        var g = await NewGroup(b, c);
        await w.Conversations.RemoveMemberAsync(owner.Id, g.Id, c.Id);
        var dto = await w.Conversations.AddMembersAsync(owner.Id, g.Id, new[] { c.Id });
        Assert.Contains(dto.Members, m => m.User.Id == c.Id);
    }

    // ---- remove
    [Fact]
    public async Task RemoveMember_ByOwner_RemovesAndNotifiesMembershipAndSystemMessage_BR7()
    {
        var g = await NewGroup(b, c);
        await w.Conversations.RemoveMemberAsync(owner.Id, g.Id, c.Id);

        Assert.False(w.Db.Conversations.Single(x => x.Id == g.Id).IsMember(c.Id));
        await w.Notifier.Received().NotifyMembershipChanged(g.Id, Arg.Any<IReadOnlyCollection<Guid>>(),
            Arg.Is<IReadOnlyCollection<Guid>>(r => r.Contains(c.Id)), Arg.Any<CancellationToken>());
        Assert.Equal(2, w.Db.Messages.Count);
        await Assert.ThrowsAsync<ForbiddenException>(() => w.SendText(c, g.Id, "still here?"));
    }

    [Fact]
    public async Task RemoveMember_ByPlainMember_ThrowsForbidden_BR7()
    {
        var g = await NewGroup(b, c);
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Conversations.RemoveMemberAsync(b.Id, g.Id, c.Id));
    }

    [Fact]
    public async Task RemoveMember_AdminCannotRemoveAdminOrOwner()
    {
        var g = await NewGroup(b, c);
        await w.Conversations.SetRoleAsync(owner.Id, g.Id, b.Id, MemberRole.Admin);
        await w.Conversations.SetRoleAsync(owner.Id, g.Id, c.Id, MemberRole.Admin);
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Conversations.RemoveMemberAsync(b.Id, g.Id, c.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Conversations.RemoveMemberAsync(b.Id, g.Id, owner.Id));
    }

    [Fact]
    public async Task RemoveMember_NonMemberTarget_ThrowsNotFound()
    {
        var g = await NewGroup(b);
        await Assert.ThrowsAsync<NotFoundException>(() => w.Conversations.RemoveMemberAsync(owner.Id, g.Id, d.Id));
    }

    // ---- leave
    [Fact]
    public async Task Leave_PlainMember_PostsSystemMessageAndBlocksFurtherAccess()
    {
        var g = await NewGroup(b, c);
        await w.Conversations.LeaveAsync(b.Id, g.Id);
        Assert.False(await w.Conversations.IsMemberAsync(b.Id, g.Id));
        Assert.Equal(2, w.Db.Messages.Count);
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Messages.GetHistoryAsync(b.Id, g.Id, null, 30));
    }

    [Fact]
    public async Task Leave_OwnerTransfersToOldestMember_BR7()
    {
        var g = await NewGroup(b, c);
        var conv = w.Db.Conversations.Single(x => x.Id == g.Id);
        World.SetJoinedAt(conv, b.Id, w.Clock.UtcNow.AddDays(1));   // bob joined later
        World.SetJoinedAt(conv, c.Id, w.Clock.UtcNow.AddHours(-5)); // carol is the oldest

        await w.Conversations.LeaveAsync(owner.Id, g.Id);

        Assert.Equal(MemberRole.Owner, conv.GetMember(c.Id)!.Role);
        Assert.Equal(MemberRole.Member, conv.GetMember(b.Id)!.Role);
        Assert.False(conv.IsMember(owner.Id));
        Assert.Equal(3, w.Db.Messages.Count);   // created + left + new owner
    }

    [Fact]
    public async Task Leave_DirectConversation_ThrowsValidation()
    {
        var direct = await w.Conversations.GetOrCreateDirectAsync(owner.Id, b.Id);
        await Assert.ThrowsAsync<ValidationException>(() => w.Conversations.LeaveAsync(owner.Id, direct.Id));
    }

    [Fact]
    public async Task Leave_NonMember_ThrowsForbidden()
    {
        var g = await NewGroup(b);
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Conversations.LeaveAsync(d.Id, g.Id));
    }

    // ---- roles
    [Fact]
    public async Task SetRole_ByOwner_PromotesAdmin_BR7()
    {
        var g = await NewGroup(b);
        await w.Conversations.SetRoleAsync(owner.Id, g.Id, b.Id, MemberRole.Admin);
        Assert.Equal(MemberRole.Admin, w.Db.Conversations.Single(x => x.Id == g.Id).GetMember(b.Id)!.Role);
    }

    [Fact]
    public async Task SetRole_ByAdmin_ThrowsForbidden()
    {
        var g = await NewGroup(b, c);
        await w.Conversations.SetRoleAsync(owner.Id, g.Id, b.Id, MemberRole.Admin);
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Conversations.SetRoleAsync(b.Id, g.Id, c.Id, MemberRole.Admin));
    }

    [Fact]
    public async Task SetRole_TransferOwnership_DemotesPreviousOwnerToAdmin()
    {
        var g = await NewGroup(b);
        await w.Conversations.SetRoleAsync(owner.Id, g.Id, b.Id, MemberRole.Owner);
        var conv = w.Db.Conversations.Single(x => x.Id == g.Id);
        Assert.Equal(MemberRole.Owner, conv.GetMember(b.Id)!.Role);
        Assert.Equal(MemberRole.Admin, conv.GetMember(owner.Id)!.Role);
    }

    // ---- update
    [Fact]
    public async Task UpdateGroup_Rename_ByAdmin_PostsSystemMessage()
    {
        var g = await NewGroup(b);
        var dto = await w.Conversations.UpdateGroupAsync(owner.Id, g.Id, new UpdateGroupDto("New name"));
        Assert.Equal("New name", dto.Name);
        Assert.Contains("New name", w.Db.Messages.Last().Content);
    }

    [Fact]
    public async Task UpdateGroup_ByPlainMember_ThrowsForbidden()
    {
        var g = await NewGroup(b);
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Conversations.UpdateGroupAsync(b.Id, g.Id, new UpdateGroupDto("x")));
    }

    [Fact]
    public async Task UpdateGroup_OnDirect_ThrowsValidation()
    {
        var direct = await w.Conversations.GetOrCreateDirectAsync(owner.Id, b.Id);
        await Assert.ThrowsAsync<ValidationException>(() => w.Conversations.UpdateGroupAsync(owner.Id, direct.Id, new UpdateGroupDto("x")));
    }

    [Fact]
    public async Task SetGroupAvatar_StoresAttachmentAndReplacesOld()
    {
        var g = await NewGroup(b);
        var png = FakeImageInspector.PngHeader.Concat(new byte[50]).ToArray();
        var first = await w.Conversations.SetGroupAvatarAsync(owner.Id, g.Id, new MemoryStream(png), "a.png", "image/png");
        Assert.NotNull(first.AvatarUrl);
        var second = await w.Conversations.SetGroupAvatarAsync(owner.Id, g.Id, new MemoryStream(png), "b.png", "image/png");
        Assert.NotEqual(first.AvatarUrl, second.AvatarUrl);
        Assert.Single(w.Db.Attachments);
        Assert.Single(w.FileStore.Files);
    }

    // ---- read receipts
    [Fact]
    public async Task MarkRead_AdvancesAndNotifies_BR11()
    {
        var direct = await w.Conversations.GetOrCreateDirectAsync(owner.Id, b.Id);
        var m = await w.SendText(owner, direct.Id, "hi");
        await w.Conversations.MarkReadAsync(b.Id, direct.Id, m.Id);

        Assert.Equal(m.Id, w.Db.Conversations.Single(x => x.Id == direct.Id).GetMember(b.Id)!.LastReadMessageId);
        await w.Notifier.Received(1).NotifyRead(direct.Id, b.Id, m.Id, Arg.Any<CancellationToken>());
        Assert.Equal(0, (await w.Conversations.ListAsync(b.Id)).Single(x => x.Id == direct.Id).UnreadCount);
    }

    [Fact]
    public async Task MarkRead_NeverMovesBackwards_BR11()
    {
        var direct = await w.Conversations.GetOrCreateDirectAsync(owner.Id, b.Id);
        var m1 = await w.SendText(owner, direct.Id, "1");
        var m2 = await w.SendText(owner, direct.Id, "2");
        await w.Conversations.MarkReadAsync(b.Id, direct.Id, m2.Id);
        await w.Conversations.MarkReadAsync(b.Id, direct.Id, m1.Id);
        Assert.Equal(m2.Id, w.Db.Conversations.Single(x => x.Id == direct.Id).GetMember(b.Id)!.LastReadMessageId);
        await w.Notifier.Received(1).NotifyRead(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkRead_ByNonMember_ThrowsForbidden()
    {
        var direct = await w.Conversations.GetOrCreateDirectAsync(owner.Id, b.Id);
        var m = await w.SendText(owner, direct.Id, "hi");
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Conversations.MarkReadAsync(c.Id, direct.Id, m.Id));
    }

    [Fact]
    public async Task MarkRead_MessageFromOtherConversation_ThrowsNotFound()
    {
        var direct = await w.Conversations.GetOrCreateDirectAsync(owner.Id, b.Id);
        var other = await w.Conversations.GetOrCreateDirectAsync(owner.Id, c.Id);
        var m = await w.SendText(owner, other.Id, "hi");
        await Assert.ThrowsAsync<NotFoundException>(() => w.Conversations.MarkReadAsync(owner.Id, direct.Id, m.Id));
    }

    [Fact]
    public async Task Get_NonMember_ThrowsForbidden_UnknownNotFound()
    {
        var g = await NewGroup(b);
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Conversations.GetAsync(d.Id, g.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => w.Conversations.GetAsync(d.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task RemoveMember_RemovedUserGetsConversationChangedWithoutThemselves()
    {
        var g = await NewGroup(b, c);
        await w.Conversations.RemoveMemberAsync(owner.Id, g.Id, c.Id);
        await w.Notifier.Received().NotifyConversationChanged(c.Id,
            Arg.Is<ConversationDto>(x => x.Id == g.Id && x.Members.All(m => m.User.Id != c.Id) && x.Members.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Leave_LeavingUserGetsConversationChangedWithoutThemselves()
    {
        var g = await NewGroup(b, c);
        await w.Conversations.LeaveAsync(c.Id, g.Id);
        await w.Notifier.Received().NotifyConversationChanged(c.Id,
            Arg.Is<ConversationDto>(x => x.Id == g.Id && x.Members.All(m => m.User.Id != c.Id)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetRole_UndefinedEnumValue_ThrowsValidation()
    {
        var g = await NewGroup(b, c);
        await Assert.ThrowsAsync<ValidationException>(() => w.Conversations.SetRoleAsync(owner.Id, g.Id, b.Id, (MemberRole)99));
    }

    [Fact]
    public async Task IsMember_ActiveOnly()
    {
        var g = await NewGroup(b);
        Assert.True(await w.Conversations.IsMemberAsync(b.Id, g.Id));
        Assert.False(await w.Conversations.IsMemberAsync(c.Id, g.Id));
    }
}

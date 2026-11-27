using Messenger.Domain.Abstractions;
using Messenger.Domain.Dtos;
using Messenger.Domain.Enums;
using Messenger.Domain.Exceptions;
using Messenger.Domain.Tests.TestKit;
using NSubstitute;

namespace Messenger.Domain.Tests;

public class FriendServiceTests
{
    private readonly World w = new();

    [Fact]
    public async Task SendRequest_Valid_CreatesPendingAndNotifiesTarget()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob");
        var dto = await w.Friends.SendRequestAsync(a.Id, b.Id);

        Assert.Equal(FriendshipStatus.Pending, dto.Status);
        Assert.Single(w.Db.Friendships);
        await w.Notifier.Received(1).NotifyFriendEvent(b.Id, FriendEventTypes.Received, Arg.Any<FriendshipDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendRequestByUsername_IsCaseInsensitive()
    {
        var a = w.AddUser("alice"); w.AddUser("Bob");
        var dto = await w.Friends.SendRequestByUsernameAsync(a.Id, "BOB");
        Assert.Equal("Bob", dto.Addressee.Username);
    }

    [Fact]
    public async Task SendRequest_ToSelf_ThrowsValidation()
    {
        var a = w.AddUser("alice");
        await Assert.ThrowsAsync<ValidationException>(() => w.Friends.SendRequestAsync(a.Id, a.Id));
    }

    [Fact]
    public async Task SendRequest_UnknownUser_ThrowsNotFound()
    {
        var a = w.AddUser("alice");
        await Assert.ThrowsAsync<NotFoundException>(() => w.Friends.SendRequestAsync(a.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task SendRequest_WhenBlockedByTarget_ThrowsForbidden_BR2()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob");
        w.Block(b, a);
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Friends.SendRequestAsync(a.Id, b.Id));
    }

    [Fact]
    public async Task SendRequest_WhenIBlockedTarget_ThrowsForbidden()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob");
        w.Block(a, b);
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Friends.SendRequestAsync(a.Id, b.Id));
    }

    [Fact]
    public async Task SendRequest_DuplicatePending_ThrowsConflict_BR3()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob");
        await w.Friends.SendRequestAsync(a.Id, b.Id);
        await Assert.ThrowsAsync<ConflictException>(() => w.Friends.SendRequestAsync(a.Id, b.Id));
        Assert.Single(w.Db.Friendships);
    }

    [Fact]
    public async Task SendRequest_AlreadyFriends_ThrowsConflict_BR3()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob");
        w.MakeFriends(a, b);
        await Assert.ThrowsAsync<ConflictException>(() => w.Friends.SendRequestAsync(a.Id, b.Id));
    }

    [Fact]
    public async Task SendRequest_WhenReverseRequestPending_AutoAcceptsAndCreatesDirectRoom_BR3_BR4()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob");
        await w.Friends.SendRequestAsync(a.Id, b.Id);

        var dto = await w.Friends.SendRequestAsync(b.Id, a.Id);

        Assert.Equal(FriendshipStatus.Accepted, dto.Status);
        Assert.Single(w.Db.Friendships);                       // still ONE record for the pair
        Assert.Single(w.Db.Conversations);
        Assert.Equal(Domain.Entities.Conversation.BuildDirectKey(a.Id, b.Id), w.Db.Conversations[0].DirectKey);
        await w.Notifier.Received().NotifyFriendEvent(a.Id, FriendEventTypes.Accepted, Arg.Any<FriendshipDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Accept_ByAddressee_SetsAcceptedCreatesDirectConversationAndNotifiesRequester()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob");
        var req = await w.Friends.SendRequestAsync(a.Id, b.Id);

        await w.Friends.AcceptAsync(b.Id, req.Id);

        Assert.Equal(FriendshipStatus.Accepted, w.Db.Friendships.Single().Status);
        var conv = w.Db.Conversations.Single();
        Assert.Equal(ConversationType.Direct, conv.Type);
        Assert.True(conv.IsMember(a.Id) && conv.IsMember(b.Id));
        await w.Notifier.Received().NotifyFriendEvent(a.Id, FriendEventTypes.Accepted, Arg.Any<FriendshipDto>(), Arg.Any<CancellationToken>());
        await w.Notifier.Received().NotifyMembershipChanged(conv.Id, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Accept_ReusesExistingDirectConversation_BR4()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob");
        var existing = w.MakeFriends(a, b);
        w.Db.Friendships.Clear();                         // simulate: unfriended earlier, room stays
        var req = await w.Friends.SendRequestAsync(a.Id, b.Id);

        await w.Friends.AcceptAsync(b.Id, req.Id);

        Assert.Same(existing, w.Db.Conversations.Single());
    }

    [Fact]
    public async Task Accept_ByRequester_ThrowsForbidden()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob");
        var req = await w.Friends.SendRequestAsync(a.Id, b.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Friends.AcceptAsync(a.Id, req.Id));
    }

    [Fact]
    public async Task Accept_Twice_ThrowsConflict()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob");
        var req = await w.Friends.SendRequestAsync(a.Id, b.Id);
        await w.Friends.AcceptAsync(b.Id, req.Id);
        await Assert.ThrowsAsync<ConflictException>(() => w.Friends.AcceptAsync(b.Id, req.Id));
    }

    [Fact]
    public async Task Accept_UnknownRequest_ThrowsNotFound()
    {
        var b = w.AddUser("bob");
        await Assert.ThrowsAsync<NotFoundException>(() => w.Friends.AcceptAsync(b.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task Decline_ByAddressee_DeletesRecord()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob");
        var req = await w.Friends.SendRequestAsync(a.Id, b.Id);
        await w.Friends.DeclineAsync(b.Id, req.Id);
        Assert.Empty(w.Db.Friendships);
    }

    [Fact]
    public async Task Decline_ByRequester_ThrowsForbidden()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob");
        var req = await w.Friends.SendRequestAsync(a.Id, b.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Friends.DeclineAsync(a.Id, req.Id));
    }

    [Fact]
    public async Task Cancel_ByRequester_DeletesRecord()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob");
        var req = await w.Friends.SendRequestAsync(a.Id, b.Id);
        await w.Friends.CancelAsync(a.Id, req.Id);
        Assert.Empty(w.Db.Friendships);
    }

    [Fact]
    public async Task Cancel_ByAddressee_ThrowsForbidden()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob");
        var req = await w.Friends.SendRequestAsync(a.Id, b.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Friends.CancelAsync(b.Id, req.Id));
    }

    [Fact]
    public async Task RemoveFriend_DeletesFriendshipKeepsRoomAndHistory_BR5()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob");
        var conv = w.MakeFriends(a, b);
        await w.SendText(a, conv.Id, "hello");

        await w.Friends.RemoveFriendAsync(a.Id, b.Id);

        Assert.Empty(w.Db.Friendships);
        Assert.Single(w.Db.Conversations);
        Assert.Single(w.Db.Messages);
        var history = await w.Messages.GetHistoryAsync(b.Id, conv.Id, null, 30);
        Assert.Single(history);
        await w.Notifier.Received().NotifyFriendRemoved(b.Id, a.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveFriend_MakesDirectRoomReadOnlyInConversationList_BR5()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob");
        w.MakeFriends(a, b);
        Assert.False((await w.Conversations.ListAsync(a.Id)).Single().IsReadOnly);

        await w.Friends.RemoveFriendAsync(a.Id, b.Id);

        Assert.True((await w.Conversations.ListAsync(a.Id)).Single().IsReadOnly);
    }

    [Fact]
    public async Task RemoveFriend_NotFriends_ThrowsNotFound()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob");
        await Assert.ThrowsAsync<NotFoundException>(() => w.Friends.RemoveFriendAsync(a.Id, b.Id));
    }

    [Fact]
    public async Task ListFriends_ReturnsPresenceAndConversationId()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob"); var c = w.AddUser("carol");
        var conv = w.MakeFriends(a, b); w.MakeFriends(a, c);
        w.Presence.Online.Add(b.Id);

        var list = await w.Friends.ListFriendsAsync(a.Id);

        Assert.Equal(2, list.Count);
        var bob = list.Single(f => f.User.Id == b.Id);
        Assert.True(bob.IsOnline);
        Assert.Equal(conv.Id, bob.ConversationId);
        Assert.False(list.Single(f => f.User.Id == c.Id).IsOnline);
    }

    [Fact]
    public async Task ListRequests_SplitsIncomingAndOutgoing()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob"); var c = w.AddUser("carol");
        await w.Friends.SendRequestAsync(a.Id, b.Id);
        await w.Friends.SendRequestAsync(c.Id, a.Id);

        var r = await w.Friends.ListRequestsAsync(a.Id);

        Assert.Equal(b.Id, r.Outgoing.Single().Addressee.Id);
        Assert.Equal(c.Id, r.Incoming.Single().Requester.Id);
    }

    [Fact]
    public async Task AreFriends_OnlyForAcceptedAndNotSelf()
    {
        var a = w.AddUser("alice"); var b = w.AddUser("bob"); var c = w.AddUser("carol");
        w.MakeFriends(a, b);
        await w.Friends.SendRequestAsync(a.Id, c.Id);
        Assert.True(await w.Friends.AreFriendsAsync(b.Id, a.Id));
        Assert.False(await w.Friends.AreFriendsAsync(a.Id, c.Id));
        Assert.False(await w.Friends.AreFriendsAsync(a.Id, a.Id));
    }
}

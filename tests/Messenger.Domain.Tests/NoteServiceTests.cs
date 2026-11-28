using Messenger.Domain.Entities;
using Messenger.Domain.Exceptions;
using Messenger.Domain.Tests.TestKit;
using NSubstitute;

namespace Messenger.Domain.Tests;

public class NoteServiceTests
{
    private readonly World w = new();
    private readonly User a, b, stranger;
    private readonly Conversation direct;

    public NoteServiceTests()
    {
        a = w.AddUser("alice"); b = w.AddUser("bob"); stranger = w.AddUser("carol");
        direct = w.MakeFriends(a, b);
    }

    [Fact]
    public async Task Set_TrimsSavesAndNotifiesFriendsAndSelf()
    {
        var n = await w.Notes.SetAsync(a.Id, "  ไปทะเลกัน  ");
        Assert.Equal("ไปทะเลกัน", n.Content);
        Assert.Equal(w.Clock.UtcNow + Note.Lifetime, n.ExpiresAt);
        await w.Notifier.Received(1).NotifyNotesChanged(
            Arg.Is<IEnumerable<Guid>>(ids => ids.ToHashSet().SetEquals(new[] { a.Id, b.Id })), a.Id, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Set_Empty_Throws(string? content)
        => await Assert.ThrowsAsync<ValidationException>(() => w.Notes.SetAsync(a.Id, content));

    [Fact]
    public async Task Set_Over60Chars_Throws()
        => await Assert.ThrowsAsync<ValidationException>(() => w.Notes.SetAsync(a.Id, new string('x', Note.MaxLength + 1)));

    [Fact]
    public async Task Set_Again_ReplacesPreviousNote()
    {
        await w.Notes.SetAsync(a.Id, "first");
        await w.Notes.SetAsync(a.Id, "second");
        Assert.Single(w.Db.Notes);
        Assert.Equal("second", w.Db.Notes[0].Content);
    }

    [Fact]
    public async Task List_ShowsOwnFirstThenFriends_HidesStrangersAndExpired()
    {
        await w.Notes.SetAsync(b.Id, "bob note");
        w.Clock.Advance(TimeSpan.FromMinutes(1));
        await w.Notes.SetAsync(a.Id, "alice note");
        await w.Notes.SetAsync(stranger.Id, "stranger note");

        var list = await w.Notes.ListAsync(a.Id);
        Assert.Equal(new[] { "alice note", "bob note" }, list.Select(n => n.Content));

        w.Clock.Advance(Note.Lifetime);
        Assert.Empty(await w.Notes.ListAsync(a.Id));
    }

    [Fact]
    public async Task List_HidesBlockedFriend()
    {
        await w.Notes.SetAsync(b.Id, "bob note");
        w.Block(b, a);
        Assert.Empty(await w.Notes.ListAsync(a.Id));
    }

    [Fact]
    public async Task Delete_RemovesNoteAndNotifies()
    {
        await w.Notes.SetAsync(a.Id, "bye");
        w.Notifier.ClearReceivedCalls();
        await w.Notes.DeleteAsync(a.Id);
        Assert.Empty(w.Db.Notes);
        await w.Notifier.Received(1).NotifyNotesChanged(Arg.Any<IEnumerable<Guid>>(), a.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reply_SendsQuotedMessageIntoDirectChat()
    {
        var note = await w.Notes.SetAsync(b.Id, "หิวข้าว");
        var m = await w.Notes.ReplyAsync(a.Id, note.Id, " ไปกินกัน ");
        Assert.Equal(direct.Id, m.ConversationId);
        Assert.Equal("ไปกินกัน", m.Content);
        Assert.Equal("หิวข้าว", m.NoteReply?.Content);
        Assert.Equal(m.Id, direct.LastMessageId);
    }

    [Fact]
    public async Task Reply_QuoteSurvivesNoteExpiry_ButIsClearedOnUnsend()
    {
        var note = await w.Notes.SetAsync(b.Id, "hello");
        var m = await w.Notes.ReplyAsync(a.Id, note.Id, "hi");
        w.Clock.Advance(Note.Lifetime + TimeSpan.FromHours(1));
        Assert.Equal("hello", w.Db.Messages.Single(x => x.Id == m.Id).NoteReplyContent);

        w.Clock.UtcNow = w.Db.Messages.Single(x => x.Id == m.Id).CreatedAt; // back inside the unsend window
        await w.Messages.UnsendAsync(a.Id, m.Id);
        Assert.Null(w.Db.Messages.Single(x => x.Id == m.Id).NoteReplyContent);
    }

    [Fact]
    public async Task Reply_OwnNote_Throws()
    {
        var note = await w.Notes.SetAsync(a.Id, "mine");
        await Assert.ThrowsAsync<ValidationException>(() => w.Notes.ReplyAsync(a.Id, note.Id, "hi"));
    }

    [Fact]
    public async Task Reply_ExpiredNote_NotFound()
    {
        var note = await w.Notes.SetAsync(b.Id, "old");
        w.Clock.Advance(Note.Lifetime);
        await Assert.ThrowsAsync<NotFoundException>(() => w.Notes.ReplyAsync(a.Id, note.Id, "hi"));
    }

    [Fact]
    public async Task Reply_NotFriend_Forbidden()
    {
        var note = await w.Notes.SetAsync(stranger.Id, "who");
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Notes.ReplyAsync(a.Id, note.Id, "hi"));
    }

    [Fact]
    public async Task Reply_Blocked_Forbidden()
    {
        var note = await w.Notes.SetAsync(b.Id, "hey");
        w.Block(a, b);
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Notes.ReplyAsync(a.Id, note.Id, "hi"));
    }

    [Fact]
    public async Task Reply_EmptyText_Throws()
    {
        var note = await w.Notes.SetAsync(b.Id, "hey");
        await Assert.ThrowsAsync<ValidationException>(() => w.Notes.ReplyAsync(a.Id, note.Id, "  "));
    }
}

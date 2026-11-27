using Messenger.Domain.Dtos;
using Messenger.Domain.Entities;
using Messenger.Domain.Enums;
using Messenger.Domain.Exceptions;
using Messenger.Domain.Tests.TestKit;
using NSubstitute;

namespace Messenger.Domain.Tests;

public class MessageServiceTests
{
    private readonly World w = new();
    private readonly User a, b;
    private readonly Conversation direct;

    public MessageServiceTests()
    {
        a = w.AddUser("alice"); b = w.AddUser("bob");
        direct = w.MakeFriends(a, b);
    }

    private Task<MessageDto> Send(User u, SendMessageDto dto) => w.Messages.SendAsync(u.Id, direct.Id, dto);

    [Fact]
    public async Task Send_TextToFriend_SavesNotifiesBothMembersAndUpdatesLastMessage()
    {
        var m = await w.SendText(a, direct.Id, "  hi  ");
        Assert.Equal("hi", m.Content);
        Assert.Equal(m.Id, direct.LastMessageId);
        await w.Notifier.Received(1).NotifyMessageReceived(
            Arg.Is<IEnumerable<Guid>>(ids => ids.OrderBy(x => x).SequenceEqual(new[] { a.Id, b.Id }.OrderBy(x => x))),
            Arg.Any<MessageDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_ToNonFriendInDirect_ThrowsForbidden_BR2()
    {
        w.Db.Friendships.Clear();
        await Assert.ThrowsAsync<ForbiddenException>(() => w.SendText(a, direct.Id, "hi"));
    }

    [Fact]
    public async Task Send_WhenBlocked_ThrowsForbidden_BR2()
    {
        w.Block(b, a);
        await Assert.ThrowsAsync<ForbiddenException>(() => w.SendText(a, direct.Id, "hi"));
    }

    [Fact]
    public async Task Send_AfterUnfriend_DirectRoomIsReadOnly_BR5()
    {
        await w.SendText(a, direct.Id, "before");
        await w.Friends.RemoveFriendAsync(a.Id, b.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => w.SendText(b, direct.Id, "after"));
    }

    [Fact]
    public async Task Send_ByNonMember_ThrowsForbidden()
    {
        var c = w.AddUser("carol");
        await Assert.ThrowsAsync<ForbiddenException>(() => w.SendText(c, direct.Id, "hi"));
    }

    [Fact]
    public async Task Send_UnknownConversation_ThrowsNotFound()
        => await Assert.ThrowsAsync<NotFoundException>(() => w.Messages.SendAsync(a.Id, Guid.NewGuid(), new SendMessageDto(MessageType.Text, "x", null, null, null)));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Send_EmptyText_ThrowsValidation_BR10(string? text)
        => await Assert.ThrowsAsync<ValidationException>(() => w.SendText(a, direct.Id, text!));

    [Fact]
    public async Task Send_Text2000Chars_Ok_BR10() => await w.SendText(a, direct.Id, new string('x', 2000));

    [Fact]
    public async Task Send_Text2001Chars_ThrowsValidation_BR10()
        => await Assert.ThrowsAsync<ValidationException>(() => w.SendText(a, direct.Id, new string('x', 2001)));

    [Fact]
    public async Task Send_SystemTypeFromClient_ThrowsValidation()
        => await Assert.ThrowsAsync<ValidationException>(() => Send(a, new SendMessageDto(MessageType.System, "x", null, null, null)));

    private async Task<Guid> Upload(User by)
    {
        var bytes = FakeImageInspector.PngHeader.Concat(new byte[100]).ToArray();
        return (await w.Files.SaveImageAsync(by.Id, new MemoryStream(bytes), "p.png", "image/png")).AttachmentId;
    }

    [Fact]
    public async Task Send_ImageWithCaption_Ok_AndUrlPointsToFile()
    {
        var id = await Upload(a);
        var m = await Send(a, new SendMessageDto(MessageType.Image, "cap", id, null, null));
        Assert.Equal($"/api/v1/files/{id}", m.AttachmentUrl);
        Assert.Equal("cap", m.Content);
    }

    [Fact]
    public async Task Send_ImageCaptionOver500_ThrowsValidation_BR10()
    {
        var id = await Upload(a);
        await Assert.ThrowsAsync<ValidationException>(() => Send(a, new SendMessageDto(MessageType.Image, new string('c', 501), id, null, null)));
    }

    [Fact]
    public async Task Send_ImageWithoutAttachment_ThrowsValidation()
        => await Assert.ThrowsAsync<ValidationException>(() => Send(a, new SendMessageDto(MessageType.Image, null, null, null, null)));

    [Fact]
    public async Task Send_ImageUploadedBySomeoneElse_ThrowsForbidden()
    {
        var id = await Upload(b);
        await Assert.ThrowsAsync<ForbiddenException>(() => Send(a, new SendMessageDto(MessageType.Image, null, id, null, null)));
    }

    [Fact]
    public async Task Send_Sticker_Ok_UnknownStickerNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Send(a, new SendMessageDto(MessageType.Sticker, null, null, 99, null)));
        var sticker = (Sticker)Activator.CreateInstance(typeof(Sticker), true)!;
        typeof(Sticker).GetProperty("Id")!.SetValue(sticker, 5);
        typeof(Sticker).GetProperty("Code")!.SetValue(sticker, "smile");
        typeof(Sticker).GetProperty("ImagePath")!.SetValue(sticker, "/stickers/smile.png");
        w.Db.Stickers.Add(sticker);
        var m = await Send(a, new SendMessageDto(MessageType.Sticker, null, null, 5, null));
        Assert.Equal(new StickerDto(5, "smile", "/stickers/smile.png"), m.Sticker);
    }

    [Fact]
    public async Task Send_ReplyToMessageInOtherConversation_ThrowsValidation()
    {
        var c = w.AddUser("carol"); var other = w.MakeFriends(a, c);
        var foreign = await w.SendText(a, other.Id, "x");
        await Assert.ThrowsAsync<ValidationException>(() => Send(a, new SendMessageDto(MessageType.Text, "re", null, null, foreign.Id)));
    }

    [Fact]
    public async Task Send_Reply_IncludesPreview()
    {
        var first = await w.SendText(a, direct.Id, "orig");
        var m = await Send(b, new SendMessageDto(MessageType.Text, "re", null, null, first.Id));
        Assert.Equal("orig", m.ReplyTo!.Content);
        Assert.Equal("alice", m.ReplyTo.SenderDisplayName);
    }

    [Fact]
    public async Task History_ByNonMember_ThrowsForbidden()
    {
        var c = w.AddUser("carol");
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Messages.GetHistoryAsync(c.Id, direct.Id, null, 30));
    }

    [Fact]
    public async Task History_CursorPagination_NewestFirst()
    {
        for (var i = 1; i <= 5; i++) await w.SendText(a, direct.Id, $"m{i}");
        var p1 = await w.Messages.GetHistoryAsync(b.Id, direct.Id, null, 2);
        Assert.Equal(new[] { "m5", "m4" }, p1.Select(m => m.Content));
        var p2 = await w.Messages.GetHistoryAsync(b.Id, direct.Id, p1[^1].Id, 2);
        Assert.Equal(new[] { "m3", "m2" }, p2.Select(m => m.Content));
    }

    [Fact]
    public async Task History_DefaultLimitUsesPageSize()
    {
        for (var i = 0; i < 35; i++) await w.SendText(a, direct.Id, "m");
        Assert.Equal(30, (await w.Messages.GetHistoryAsync(a.Id, direct.Id, null, 0)).Count);
    }

    [Fact]
    public async Task Unsend_ByOwnerWithinWindow_MasksContentAndNotifies_BR8_BR9()
    {
        var m = await w.SendText(a, direct.Id, "secret");
        await w.Messages.UnsendAsync(a.Id, m.Id);

        var h = (await w.Messages.GetHistoryAsync(b.Id, direct.Id, null, 30)).Single();
        Assert.True(h.IsUnsent);
        Assert.Null(h.Content);
        await w.Notifier.Received(1).NotifyMessageUnsent(direct.Id, m.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unsend_ImageMessage_DeletesFileAndAttachment_BR9()
    {
        var id = await Upload(a);
        var m = await Send(a, new SendMessageDto(MessageType.Image, "c", id, null, null));
        Assert.Single(w.FileStore.Files);

        await w.Messages.UnsendAsync(a.Id, m.Id);

        Assert.Empty(w.FileStore.Files);
        Assert.Empty(w.Db.Attachments);
        var h = (await w.Messages.GetHistoryAsync(b.Id, direct.Id, null, 30)).Single();
        Assert.Null(h.AttachmentUrl);
        Assert.Null(h.Content);
    }

    [Fact]
    public async Task Unsend_ByOtherUser_ThrowsForbidden_BR8()
    {
        var m = await w.SendText(a, direct.Id, "x");
        await Assert.ThrowsAsync<ForbiddenException>(() => w.Messages.UnsendAsync(b.Id, m.Id));
    }

    [Fact]
    public async Task Unsend_After24Hours_ThrowsConflict_BR8()
    {
        var m = await w.SendText(a, direct.Id, "x");
        w.Clock.Advance(TimeSpan.FromHours(24).Add(TimeSpan.FromMinutes(1)));
        await Assert.ThrowsAsync<ConflictException>(() => w.Messages.UnsendAsync(a.Id, m.Id));
    }

    [Fact]
    public async Task Unsend_JustInsideWindow_Ok_BR8()
    {
        var m = await w.SendText(a, direct.Id, "x");
        w.Clock.Advance(TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(59)));
        await w.Messages.UnsendAsync(a.Id, m.Id);
    }

    [Fact]
    public async Task Unsend_Twice_ThrowsConflict()
    {
        var m = await w.SendText(a, direct.Id, "x");
        await w.Messages.UnsendAsync(a.Id, m.Id);
        await Assert.ThrowsAsync<ConflictException>(() => w.Messages.UnsendAsync(a.Id, m.Id));
    }

    [Fact]
    public async Task Unsend_UnknownMessage_ThrowsNotFound()
        => await Assert.ThrowsAsync<NotFoundException>(() => w.Messages.UnsendAsync(a.Id, 999));

    [Fact]
    public async Task Unsend_ConfigurableWindow_IsRespected()
    {
        w.Chat.UnsendWindowHours = 1;
        var m = await w.SendText(a, direct.Id, "x");
        w.Clock.Advance(TimeSpan.FromHours(2));
        await Assert.ThrowsAsync<ConflictException>(() => w.Messages.UnsendAsync(a.Id, m.Id));
    }

    [Fact]
    public async Task UnreadCount_CountsOthersMessagesAndExcludesUnsent_BR11()
    {
        var m1 = await w.SendText(a, direct.Id, "1");
        await w.SendText(a, direct.Id, "2");
        await w.SendText(b, direct.Id, "mine");
        Assert.Equal(2, (await w.Conversations.ListAsync(b.Id)).Single().UnreadCount);

        await w.Messages.UnsendAsync(a.Id, m1.Id);
        Assert.Equal(1, (await w.Conversations.ListAsync(b.Id)).Single().UnreadCount);
    }

    [Fact]
    public async Task UnsentMessage_AsLastMessage_IsMaskedInConversationList()
    {
        var m = await w.SendText(a, direct.Id, "x");
        await w.Messages.UnsendAsync(a.Id, m.Id);
        var last = (await w.Conversations.ListAsync(b.Id)).Single().LastMessage!;
        Assert.True(last.IsUnsent);
        Assert.Null(last.Content);
    }

    [Fact]
    public async Task Send_AttachmentAlreadyUsedByMessage_ThrowsConflict_SingleUse()
    {
        var id = await Upload(a);
        await Send(a, new SendMessageDto(MessageType.Image, null, id, null, null));
        await Assert.ThrowsAsync<ConflictException>(() => Send(a, new SendMessageDto(MessageType.Image, null, id, null, null)));
    }

    [Fact]
    public async Task Send_AttachmentUsedAsAvatar_ThrowsConflict_SingleUse()
    {
        var png = FakeImageInspector.PngHeader.Concat(new byte[10]).ToArray();
        await w.Users.SetAvatarAsync(a.Id, new MemoryStream(png), "a.png", "image/png");
        var avatarId = w.Db.Users.Single(u => u.Id == a.Id).AvatarAttachmentId!.Value;
        await Assert.ThrowsAsync<ConflictException>(() => Send(a, new SendMessageDto(MessageType.Image, null, avatarId, null, null)));
    }

    [Fact]
    public async Task Send_SavesMessageAndConversationInsideOneTransaction()
    {
        var before = w.Db.TransactionCount;
        await w.SendText(a, direct.Id, "hi");
        Assert.Equal(before + 1, w.Db.TransactionCount);
    }
}

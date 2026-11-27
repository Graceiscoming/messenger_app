using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Messenger.Api.Contracts;
using Messenger.Domain.Dtos;
using Messenger.Domain.Enums;
using Microsoft.AspNetCore.SignalR.Client;

namespace Messenger.Api.Tests;

[Collection("api")]
public class AuthTests(ApiFixture fx)
{
    [Fact]
    public async Task Register_then_login_returns_tokens()
    {
        var u = await Api.Register(fx.Factory);
        var r = await fx.Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginDto(u.Username, u.Password), Json.Options);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var auth = await u.Read<AuthResultDto>(r);
        Assert.False(string.IsNullOrEmpty(auth.AccessToken));
        Assert.False(string.IsNullOrEmpty(auth.RefreshToken));
        Assert.Equal(u.Id, auth.User.Id);
    }

    [Fact]
    public async Task Duplicate_username_case_insensitive_returns_409()
    {
        var name = "Dup" + Guid.NewGuid().ToString("N")[..10];
        await Api.Register(fx.Factory, name);
        var r = await fx.Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new RegisterDto(name.ToLowerInvariant(), "x", Api.Pw), Json.Options);
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_without_token_returns_401()
    {
        var r = await fx.Factory.CreateClient().GetAsync("/api/v1/friends");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_returns_401()
    {
        var u = await Api.Register(fx.Factory);
        var r = await fx.Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginDto(u.Username, "wrong-password"), Json.Options);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Lockout_after_5_failed_logins_then_recovers_after_window()
    {
        var f = fx.TimeFactory;
        var u = await Api.Register(f);
        var c = f.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            var bad = await c.PostAsJsonAsync("/api/v1/auth/login", new LoginDto(u.Username, "nope"), Json.Options);
            Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        }
        // Correct password is rejected while locked out.
        var locked = await c.PostAsJsonAsync("/api/v1/auth/login", new LoginDto(u.Username, u.Password), Json.Options);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);

        fx.TimeClock.Advance(TimeSpan.FromMinutes(6));
        var ok = await c.PostAsJsonAsync("/api/v1/auth/login", new LoginDto(u.Username, u.Password), Json.Options);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Refresh_issues_new_tokens()
    {
        var c = fx.Factory.CreateClient();
        var reg = await c.PostAsJsonAsync("/api/v1/auth/register", new RegisterDto("r" + Guid.NewGuid().ToString("N")[..10], "R", Api.Pw), Json.Options);
        var auth = (await reg.Content.ReadFromJsonAsync<AuthResultDto>(Json.Options))!;
        var r = await c.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(auth.RefreshToken), Json.Options);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }
}

[Collection("api")]
public class FriendAndConversationTests(ApiFixture fx)
{
    [Fact]
    public async Task Full_flow_search_friend_direct_send_history_read()
    {
        var f = fx.Factory;
        var a = await Api.Register(f);
        var b = await Api.Register(f);

        var search = await a.Ok<List<UserDto>>(await a.Http.GetAsync($"/api/v1/users/search?username={b.Username}"), HttpStatusCode.OK);
        Assert.Contains(search, x => x.Id == b.Id);

        var fr = await a.Ok<FriendshipDto>(await a.Post("/api/v1/friends/requests", new SendFriendRequestRequest(b.Username)), HttpStatusCode.Created);
        var incoming = await b.Ok<FriendRequestsDto>(await b.Http.GetAsync("/api/v1/friends/requests"), HttpStatusCode.OK);
        Assert.Contains(incoming.Incoming, x => x.Id == fr.Id);
        await b.NoContent(await b.Post($"/api/v1/friends/requests/{fr.Id}/accept"));

        var friends = await a.Ok<List<FriendDto>>(await a.Http.GetAsync("/api/v1/friends"), HttpStatusCode.OK);
        Assert.Contains(friends, x => x.User.Id == b.Id);

        var conv = await Api.Direct(a, b);
        Assert.Equal(ConversationType.Direct, conv.Type);
        var again = await Api.Direct(b, a);
        Assert.Equal(conv.Id, again.Id);

        var sent = new List<MessageDto>();
        for (var i = 0; i < 5; i++) sent.Add(await Api.SendText(a, conv.Id, $"hello {i}"));

        var page1 = await b.Ok<List<MessageDto>>(await b.Http.GetAsync($"/api/v1/conversations/{conv.Id}/messages?limit=2"), HttpStatusCode.OK);
        Assert.Equal(2, page1.Count);
        var cursor = page1.Min(m => m.Id);
        var page2 = await b.Ok<List<MessageDto>>(await b.Http.GetAsync($"/api/v1/conversations/{conv.Id}/messages?limit=2&beforeId={cursor}"), HttpStatusCode.OK);
        Assert.Equal(2, page2.Count);
        Assert.All(page2, m => Assert.True(m.Id < cursor));
        Assert.Empty(page1.Select(m => m.Id).Intersect(page2.Select(m => m.Id)));

        var last = sent[^1];
        await b.NoContent(await b.Post($"/api/v1/conversations/{conv.Id}/read", new MarkReadRequest(last.Id)));
        var after = await a.Ok<ConversationDto>(await a.Http.GetAsync($"/api/v1/conversations/{conv.Id}"), HttpStatusCode.OK);
        Assert.Equal(last.Id, after.Members.Single(m => m.User.Id == b.Id).LastReadMessageId);
    }

    [Fact]
    public async Task Duplicate_friend_request_returns_409()
    {
        var a = await Api.Register(fx.Factory);
        var b = await Api.Register(fx.Factory);
        await a.Ok<FriendshipDto>(await a.Post("/api/v1/friends/requests", new SendFriendRequestRequest(b.Username)), HttpStatusCode.Created);
        var r = await a.Post("/api/v1/friends/requests", new SendFriendRequestRequest(b.Username));
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
    }

    [Fact]
    public async Task Requester_cannot_accept_own_request()
    {
        var a = await Api.Register(fx.Factory);
        var b = await Api.Register(fx.Factory);
        var fr = await a.Ok<FriendshipDto>(await a.Post("/api/v1/friends/requests", new SendFriendRequestRequest(b.Username)), HttpStatusCode.Created);
        var r = await a.Post($"/api/v1/friends/requests/{fr.Id}/accept");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Non_friend_cannot_open_direct_conversation()
    {
        var a = await Api.Register(fx.Factory);
        var b = await Api.Register(fx.Factory);
        var r = await a.Post("/api/v1/conversations/direct", new DirectConversationRequest(b.Id));
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode); // BR-2
    }

    [Fact]
    public async Task After_unfriend_sending_is_forbidden()
    {
        var (a, b, conv) = await Api.FriendPair(fx.Factory);
        await a.NoContent(await a.Http.DeleteAsync($"/api/v1/friends/{b.Id}"));
        var r = await a.Post($"/api/v1/conversations/{conv.Id}/messages", new SendMessageDto(MessageType.Text, "hi", null, null, null));
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Blocked_user_cannot_send_and_blocker_cannot_either()
    {
        var (a, b, conv) = await Api.FriendPair(fx.Factory);
        await a.NoContent(await a.Post($"/api/v1/users/{b.Id}/block"));
        var body = new SendMessageDto(MessageType.Text, "hi", null, null, null);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Post($"/api/v1/conversations/{conv.Id}/messages", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.Post($"/api/v1/conversations/{conv.Id}/messages", body)).StatusCode);
    }

    [Fact]
    public async Task Non_member_cannot_read_history_or_send_or_get_conversation_IDOR()
    {
        var (a, _, conv) = await Api.FriendPair(fx.Factory);
        await Api.SendText(a, conv.Id, "secret");
        var outsider = await Api.Register(fx.Factory);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.Http.GetAsync($"/api/v1/conversations/{conv.Id}/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.Http.GetAsync($"/api/v1/conversations/{conv.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await outsider.Post($"/api/v1/conversations/{conv.Id}/messages", new SendMessageDto(MessageType.Text, "x", null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.Post($"/api/v1/conversations/{conv.Id}/read", new MarkReadRequest(1))).StatusCode);
    }

    [Fact]
    public async Task Empty_and_oversized_text_rejected_400()
    {
        var (a, _, conv) = await Api.FriendPair(fx.Factory);
        var url = $"/api/v1/conversations/{conv.Id}/messages";
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Post(url, new SendMessageDto(MessageType.Text, "   ", null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Post(url, new SendMessageDto(MessageType.Text, new string('x', 2001), null, null, null))).StatusCode);
    }
}

[Collection("api")]
public class UnsendTests(ApiFixture fx)
{
    [Fact]
    public async Task Unsend_replaces_content_with_placeholder_and_removes_file()
    {
        var (a, b, conv) = await Api.FriendPair(fx.Factory);
        var att = await a.Ok<AttachmentDto>(await Api.Upload(a, Api.Png), HttpStatusCode.Created);
        var filesBefore = Directory.GetFiles(fx.UploadPath, "*", SearchOption.AllDirectories).Length;

        var img = await a.Ok<MessageDto>(await a.Post($"/api/v1/conversations/{conv.Id}/messages",
            new SendMessageDto(MessageType.Image, null, att.AttachmentId, null, null)), HttpStatusCode.Created);
        Assert.NotNull(img.AttachmentUrl);
        var txt = await Api.SendText(a, conv.Id, "to be unsent");

        await a.NoContent(await a.Http.DeleteAsync($"/api/v1/messages/{txt.Id}"));
        await a.NoContent(await a.Http.DeleteAsync($"/api/v1/messages/{img.Id}"));

        var hist = await b.Ok<List<MessageDto>>(await b.Http.GetAsync($"/api/v1/conversations/{conv.Id}/messages"), HttpStatusCode.OK);
        var t = hist.Single(m => m.Id == txt.Id);
        Assert.True(t.IsUnsent);
        Assert.Null(t.Content);
        var i = hist.Single(m => m.Id == img.Id);
        Assert.True(i.IsUnsent);
        Assert.Null(i.AttachmentUrl);

        var filesAfter = Directory.GetFiles(fx.UploadPath, "*", SearchOption.AllDirectories).Length;
        Assert.Equal(filesBefore - 1, filesAfter); // BR-9: file physically removed
        Assert.NotEqual(HttpStatusCode.OK, (await b.Http.GetAsync($"/api/v1/files/{att.AttachmentId}")).StatusCode);
    }

    [Fact]
    public async Task Cannot_unsend_someone_elses_message_403()
    {
        var (a, b, conv) = await Api.FriendPair(fx.Factory);
        var m = await Api.SendText(a, conv.Id, "mine");
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Http.DeleteAsync($"/api/v1/messages/{m.Id}")).StatusCode);
    }

    [Fact]
    public async Task Unsend_twice_returns_409_and_unknown_returns_404()
    {
        var (a, _, conv) = await Api.FriendPair(fx.Factory);
        var m = await Api.SendText(a, conv.Id, "x");
        await a.NoContent(await a.Http.DeleteAsync($"/api/v1/messages/{m.Id}"));
        Assert.Equal(HttpStatusCode.Conflict, (await a.Http.DeleteAsync($"/api/v1/messages/{m.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Http.DeleteAsync("/api/v1/messages/999999999")).StatusCode);
    }

    [Fact]
    public async Task Unsend_after_window_returns_409()
    {
        var (a, _, conv) = await Api.FriendPair(fx.TimeFactory);
        var m = await Api.SendText(a, conv.Id, "old");
        fx.TimeClock.Advance(TimeSpan.FromHours(25));
        Assert.Equal(HttpStatusCode.Conflict, (await a.Http.DeleteAsync($"/api/v1/messages/{m.Id}")).StatusCode);
    }
}

[Collection("api")]
public class FileTests(ApiFixture fx)
{
    [Fact]
    public async Task Valid_png_upload_201_and_downloadable_by_uploader()
    {
        var a = await Api.Register(fx.Factory);
        var att = await a.Ok<AttachmentDto>(await Api.Upload(a, Api.Png), HttpStatusCode.Created);
        Assert.Equal(1, att.Width);
        var get = await a.Http.GetAsync($"/api/v1/files/{att.AttachmentId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal("image/png", get.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Bad_magic_bytes_returns_400_even_with_image_content_type()
    {
        var a = await Api.Register(fx.Factory);
        var r = await Api.Upload(a, "this is not an image, just text"u8.ToArray(), "image/png", "evil.png");
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Image_over_5MB_returns_413()
    {
        var a = await Api.Register(fx.Factory);
        var big = new byte[6 * 1024 * 1024];
        Array.Copy(Api.Png, big, Api.Png.Length);
        var r = await Api.Upload(a, big);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, r.StatusCode);
    }

    [Fact]
    public async Task Upload_without_token_returns_401()
    {
        var r = await fx.Factory.CreateClient().PostAsync("/api/v1/files/images", new MultipartFormDataContent());
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Unrelated_user_cannot_download_attachment()
    {
        var a = await Api.Register(fx.Factory);
        var other = await Api.Register(fx.Factory);
        var att = await a.Ok<AttachmentDto>(await Api.Upload(a, Api.Png), HttpStatusCode.Created);
        var r = await other.Http.GetAsync($"/api/v1/files/{att.AttachmentId}");
        Assert.True(r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, $"got {(int)r.StatusCode}");
    }

    [Fact]
    public async Task Cannot_send_attachment_uploaded_by_someone_else()
    {
        var (a, b, conv) = await Api.FriendPair(fx.Factory);
        var att = await a.Ok<AttachmentDto>(await Api.Upload(a, Api.Png), HttpStatusCode.Created);
        var r = await b.Post($"/api/v1/conversations/{conv.Id}/messages", new SendMessageDto(MessageType.Image, null, att.AttachmentId, null, null));
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }
}

[Collection("api")]
public class GroupTests(ApiFixture fx)
{
    [Fact]
    public async Task Group_create_add_remove_role_leave()
    {
        var f = fx.Factory;
        var owner = await Api.Register(f);
        var m1 = await Api.Register(f);
        var m2 = await Api.Register(f);
        var m3 = await Api.Register(f);
        foreach (var x in new[] { m1, m2, m3 }) await Api.BecomeFriends(owner, x);

        var g = await Api.Group(owner, "Team " + Guid.NewGuid().ToString("N")[..6], m1, m2);
        Assert.Equal(ConversationType.Group, g.Type);
        Assert.Equal(3, g.Members.Count);
        Assert.Equal(MemberRole.Owner, g.Members.Single(m => m.User.Id == owner.Id).Role);

        // plain member cannot manage (BR-7)
        Assert.Equal(HttpStatusCode.Forbidden,
            (await m1.Post($"/api/v1/conversations/{g.Id}/members", new AddMembersRequest([m3.Id]))).StatusCode);

        // owner adds m3
        var added = await owner.Ok<ConversationDto>(await owner.Post($"/api/v1/conversations/{g.Id}/members", new AddMembersRequest([m3.Id])), HttpStatusCode.OK);
        Assert.Contains(added.Members, m => m.User.Id == m3.Id);

        // only owner changes roles
        await owner.NoContent(await owner.Put($"/api/v1/conversations/{g.Id}/members/{m1.Id}/role", new SetRoleRequest(MemberRole.Admin)));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await m1.Put($"/api/v1/conversations/{g.Id}/members/{m2.Id}/role", new SetRoleRequest(MemberRole.Admin))).StatusCode);

        // plain member cannot remove, owner can
        Assert.Equal(HttpStatusCode.Forbidden, (await m2.Http.DeleteAsync($"/api/v1/conversations/{g.Id}/members/{m3.Id}")).StatusCode);
        await owner.NoContent(await owner.Http.DeleteAsync($"/api/v1/conversations/{g.Id}/members/{m3.Id}"));
        Assert.Equal(HttpStatusCode.Forbidden, (await m3.Http.GetAsync($"/api/v1/conversations/{g.Id}/messages")).StatusCode);

        // messaging works in group, then leave
        await Api.SendText(m2, g.Id, "hello group");
        await m2.NoContent(await m2.Post($"/api/v1/conversations/{g.Id}/leave"));
        Assert.Equal(HttpStatusCode.Forbidden, (await m2.Http.GetAsync($"/api/v1/conversations/{g.Id}/messages")).StatusCode);
    }

    [Fact]
    public async Task Cannot_create_group_with_non_friend()
    {
        var owner = await Api.Register(fx.Factory);
        var stranger = await Api.Register(fx.Factory);
        var r = await owner.Post("/api/v1/conversations/group", new CreateGroupDto("g", [stranger.Id]));
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }
}

[Collection("api")]
public class RealtimeTests(ApiFixture fx)
{
    [Fact]
    public async Task MessageReceived_and_MessageUnsent_are_pushed_to_recipient()
    {
        var (a, b, conv) = await Api.FriendPair(fx.Factory);
        await using var hubB = await b.ConnectHub();
        var received = new TaskCompletionSource<MessageDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var unsent = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        hubB.On<MessageDto>("MessageReceived", m => received.TrySetResult(m));
        hubB.On<JsonElement>("MessageUnsent", e => unsent.TrySetResult(e));

        var sent = await Api.SendText(a, conv.Id, "realtime hello");
        var got = await Api.Next(received);
        Assert.Equal(sent.Id, got.Id);
        Assert.Equal("realtime hello", got.Content);
        Assert.Equal(MessageType.Text, got.Type);

        await a.NoContent(await a.Http.DeleteAsync($"/api/v1/messages/{sent.Id}"));
        var ev = await Api.Next(unsent);
        Assert.Equal(sent.Id, ev.GetProperty("messageId").GetInt64());
        Assert.Equal(conv.Id, ev.GetProperty("conversationId").GetGuid());
    }

    [Fact]
    public async Task Hub_rejects_connection_without_token()
    {
        var server = fx.Factory.Server;
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "/hubs/chat"), o =>
            {
                o.HttpMessageHandlerFactory = _ => server.CreateHandler();
                o.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            }).Build();
        await Assert.ThrowsAnyAsync<Exception>(() => hub.StartAsync());
    }

    [Fact]
    public async Task Outsider_does_not_receive_messages()
    {
        var (a, _, conv) = await Api.FriendPair(fx.Factory);
        var outsider = await Api.Register(fx.Factory);
        await using var hub = await outsider.ConnectHub();
        var got = false;
        hub.On<MessageDto>("MessageReceived", _ => got = true);
        await Api.SendText(a, conv.Id, "private");
        await Task.Delay(1000);
        Assert.False(got);
    }

    [Fact]
    public async Task FriendRequestReceived_is_pushed()
    {
        var a = await Api.Register(fx.Factory);
        var b = await Api.Register(fx.Factory);
        await using var hubB = await b.ConnectHub();
        var tcs = new TaskCompletionSource<FriendshipDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        hubB.On<FriendshipDto>("FriendRequestReceived", x => tcs.TrySetResult(x));
        await a.Ok<FriendshipDto>(await a.Post("/api/v1/friends/requests", new SendFriendRequestRequest(b.Username)), HttpStatusCode.Created);
        var fr = await Api.Next(tcs);
        Assert.Equal(a.Id, fr.Requester.Id);
    }
}

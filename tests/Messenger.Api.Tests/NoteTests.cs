using System.Net;
using System.Text.Json;
using Messenger.Api.Contracts;
using Messenger.Domain.Dtos;
using Microsoft.AspNetCore.SignalR.Client;

namespace Messenger.Api.Tests;

[Collection("api")]
public class NoteTests(ApiFixture fx)
{
    [Fact]
    public async Task Post_list_reply_and_delete_note_over_rest_and_hub()
    {
        var (a, b, conv) = await Api.FriendPair(fx.Factory);
        var outsider = await Api.Register(fx.Factory);
        await using var hubB = await b.ConnectHub();
        var changed = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        hubB.On<JsonElement>("NotesChanged", e => changed.TrySetResult(e));

        var note = await a.Ok<NoteDto>(await a.Put("/api/v1/notes/me", new SetNoteRequest("  coffee?  ")), HttpStatusCode.OK);
        Assert.Equal("coffee?", note.Content);
        Assert.Equal(a.Id, (await Api.Next(changed)).GetProperty("userId").GetGuid());

        var seenByB = await b.Ok<List<NoteDto>>(await b.Http.GetAsync("/api/v1/notes"), HttpStatusCode.OK);
        Assert.Equal(note.Id, Assert.Single(seenByB).Id);
        Assert.Empty(await outsider.Ok<List<NoteDto>>(await outsider.Http.GetAsync("/api/v1/notes"), HttpStatusCode.OK));

        var reply = await b.Ok<MessageDto>(await b.Post($"/api/v1/notes/{note.Id}/replies", new NoteReplyRequest("yes!")), HttpStatusCode.Created);
        Assert.Equal(conv.Id, reply.ConversationId);
        Assert.Equal("coffee?", reply.NoteReply?.Content);
        var hist = await a.Ok<List<MessageDto>>(await a.Http.GetAsync($"/api/v1/conversations/{conv.Id}/messages"), HttpStatusCode.OK);
        Assert.Equal("coffee?", hist.Single(m => m.Id == reply.Id).NoteReply?.Content);

        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.Post($"/api/v1/notes/{note.Id}/replies", new NoteReplyRequest("hi"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Post($"/api/v1/notes/{note.Id}/replies", new NoteReplyRequest("me"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Put("/api/v1/notes/me", new SetNoteRequest(new string('x', 61)))).StatusCode);

        await a.NoContent(await a.Http.DeleteAsync("/api/v1/notes/me"));
        Assert.Empty(await b.Ok<List<NoteDto>>(await b.Http.GetAsync("/api/v1/notes"), HttpStatusCode.OK));
        Assert.Equal(HttpStatusCode.NotFound, (await b.Post($"/api/v1/notes/{note.Id}/replies", new NoteReplyRequest("late"))).StatusCode);
    }
}

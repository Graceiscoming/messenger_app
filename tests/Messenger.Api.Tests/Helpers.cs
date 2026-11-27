using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Messenger.Api.Contracts;
using Messenger.Domain.Dtos;
using Messenger.Domain.Enums;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Messenger.Api.Tests;

public static class Json
{
    public static readonly JsonSerializerOptions Options = Create();
    private static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }
}

public sealed class TestUser
{
    private readonly AuthResultDto _auth;
    public ApiFactory Factory { get; }
    public string Password { get; }
    public HttpClient Http { get; }

    public TestUser(ApiFactory factory, AuthResultDto auth, string password)
    {
        Factory = factory;
        _auth = auth;
        Password = password;
        Http = factory.CreateClient();
        Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
    }

    public Guid Id => _auth.User.Id;
    public string Username => _auth.User.Username;
    public string Token => _auth.AccessToken;

    public Task<HttpResponseMessage> Post(string url, object? body = null) => Http.PostAsJsonAsync(url, body, Json.Options);
    public Task<HttpResponseMessage> Put(string url, object body) => Http.PutAsJsonAsync(url, body, Json.Options);

    public async Task<T> Read<T>(HttpResponseMessage r)
        => JsonSerializer.Deserialize<T>(await r.Content.ReadAsStringAsync(), Json.Options)!;

    public async Task<T> Ok<T>(HttpResponseMessage r, HttpStatusCode expected)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == expected, $"Expected {(int)expected} but got {(int)r.StatusCode}: {text}");
        return JsonSerializer.Deserialize<T>(text, Json.Options)!;
    }

    public async Task NoContent(HttpResponseMessage r)
        => Assert.True(r.StatusCode == HttpStatusCode.NoContent,
            $"Expected 204 but got {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");

    public async Task<HubConnection> ConnectHub()
    {
        var server = Factory.Server;
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "/hubs/chat"), o =>
            {
                o.HttpMessageHandlerFactory = _ => server.CreateHandler();
                o.AccessTokenProvider = () => Task.FromResult<string?>(Token);
                o.Transports = HttpTransportType.LongPolling;
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
        await hub.StartAsync();
        return hub;
    }
}

public static class Api
{
    public const string Pw = "Passw0rd!test";

    public static async Task<TestUser> Register(ApiFactory f, string? username = null, string password = Pw)
    {
        username ??= "u" + Guid.NewGuid().ToString("N")[..12];
        var c = f.CreateClient();
        var r = await c.PostAsJsonAsync("/api/v1/auth/register", new RegisterDto(username, "Name " + username, password), Json.Options);
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == HttpStatusCode.Created, $"register failed {(int)r.StatusCode}: {text}");
        return new TestUser(f, JsonSerializer.Deserialize<AuthResultDto>(text, Json.Options)!, password);
    }

    /// <summary>Sends a friend request a -> b and accepts it. Returns the friendship id.</summary>
    public static async Task<Guid> BecomeFriends(TestUser a, TestUser b)
    {
        var fr = await a.Ok<FriendshipDto>(await a.Post("/api/v1/friends/requests", new SendFriendRequestRequest(b.Username)), HttpStatusCode.Created);
        await b.NoContent(await b.Post($"/api/v1/friends/requests/{fr.Id}/accept"));
        return fr.Id;
    }

    public static async Task<ConversationDto> Direct(TestUser a, TestUser b)
        => await a.Ok<ConversationDto>(await a.Post("/api/v1/conversations/direct", new DirectConversationRequest(b.Id)), HttpStatusCode.OK);

    public static async Task<(TestUser a, TestUser b, ConversationDto conv)> FriendPair(ApiFactory f)
    {
        var a = await Register(f);
        var b = await Register(f);
        await BecomeFriends(a, b);
        return (a, b, await Direct(a, b));
    }

    public static async Task<MessageDto> SendText(TestUser u, Guid convId, string text)
        => await u.Ok<MessageDto>(await u.Post($"/api/v1/conversations/{convId}/messages",
            new SendMessageDto(MessageType.Text, text, null, null, null)), HttpStatusCode.Created);

    public static async Task<ConversationDto> Group(TestUser owner, string name, params TestUser[] members)
        => await owner.Ok<ConversationDto>(await owner.Post("/api/v1/conversations/group",
            new CreateGroupDto(name, members.Select(m => m.Id).ToList())), HttpStatusCode.Created);

    // 1x1 PNG
    public static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    public static async Task<HttpResponseMessage> Upload(TestUser u, byte[] bytes, string contentType = "image/png", string name = "a.png")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", name);
        return await u.Http.PostAsync("/api/v1/files/images", content);
    }

    public static async Task<T> Next<T>(TaskCompletionSource<T> tcs, int seconds = 10)
    {
        var done = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(seconds)));
        Assert.True(done == tcs.Task, "Timed out waiting for SignalR event");
        return await tcs.Task;
    }
}

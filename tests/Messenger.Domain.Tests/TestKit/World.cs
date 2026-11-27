using System.Reflection;
using Messenger.Domain.Abstractions;
using Messenger.Domain.Entities;
using Messenger.Domain.Enums;
using Messenger.Domain.Options;
using Messenger.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Messenger.Domain.Tests.TestKit;

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    public void Advance(TimeSpan by) => UtcNow += by;
}

public sealed class FakeHasher : IPasswordHasher
{
    public string Hash(string password) => "hash:" + password;
    public bool Verify(string hash, string password) => hash == "hash:" + password;
}

public sealed class FakeTokens : ITokenService
{
    public string CreateAccessToken(User user) => "access:" + user.Id;
    public string CreateRefreshToken() => "refresh:" + Guid.NewGuid();
    public string HashRefreshToken(string t) => "h(" + t + ")";
}

public sealed class FakePresence : IPresenceTracker
{
    public HashSet<Guid> Online { get; } = new();
    public bool IsOnline(Guid userId) => Online.Contains(userId);
    public IReadOnlyCollection<Guid> GetOnlineUsers(IEnumerable<Guid> ids) => ids.Where(Online.Contains).ToList();
}

/// <summary>Recognises PNG ("\x89PNG") only; everything else is rejected.</summary>
public sealed class FakeImageInspector : IImageInspector
{
    public static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    public ImageInfo? Inspect(Stream stream)
    {
        var head = new byte[8];
        var n = stream.Read(head, 0, 8);
        return n == 8 && head.SequenceEqual(PngHeader) ? new ImageInfo("image/png", ".png", 10, 20) : null;
    }
}

public sealed class FakeFileStore : IFileStore
{
    public Dictionary<string, byte[]> Files { get; } = new();
    public Task SaveAsync(Stream content, string storedName, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        Files[storedName] = ms.ToArray();
        return Task.CompletedTask;
    }
    public Task<Stream> OpenReadAsync(string storedName, CancellationToken ct = default)
        => Task.FromResult<Stream>(new MemoryStream(Files[storedName]));
    public Task DeleteAsync(string storedName, CancellationToken ct = default) { Files.Remove(storedName); return Task.CompletedTask; }
}

/// <summary>Composes the real Domain services (via AddDomain) over in-memory fakes.</summary>
public sealed class World
{
    public InMemoryStore Db { get; } = new();
    public FakeClock Clock { get; } = new();
    public FakePresence Presence { get; } = new();
    public FakeFileStore FileStore { get; } = new();
    public IRealtimeNotifier Notifier { get; } = Substitute.For<IRealtimeNotifier>();
    public ChatOptions Chat { get; } = new();
    public AuthOptions AuthOpts { get; } = new();
    public StorageOptions Storage { get; } = new();
    private readonly IServiceProvider _sp;

    public World()
    {
        var s = new ServiceCollection();
        s.AddDomain();
        s.AddSingleton<IUnitOfWork>(Db);
        s.AddSingleton(Db.UserRepo);
        s.AddSingleton(Db.FriendshipRepo);
        s.AddSingleton(Db.BlockRepo);
        s.AddSingleton(Db.ConversationRepo);
        s.AddSingleton(Db.MessageRepo);
        s.AddSingleton(Db.AttachmentRepo);
        s.AddSingleton(Db.StickerRepo);
        s.AddSingleton(Db.RefreshTokenRepo);
        s.AddSingleton<IClock>(Clock);
        s.AddSingleton<IPasswordHasher, FakeHasher>();
        s.AddSingleton<ITokenService, FakeTokens>();
        s.AddSingleton<IPresenceTracker>(Presence);
        s.AddSingleton<IImageInspector, FakeImageInspector>();
        s.AddSingleton<IFileStore>(FileStore);
        s.AddSingleton(Notifier);
        s.AddSingleton(Microsoft.Extensions.Options.Options.Create(Chat));
        s.AddSingleton(Microsoft.Extensions.Options.Options.Create(AuthOpts));
        s.AddSingleton(Microsoft.Extensions.Options.Options.Create(Storage));
        s.AddSingleton(Microsoft.Extensions.Options.Options.Create(new JwtOptions { AccessMinutes = 15, RefreshDays = 7 }));
        _sp = s.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = false });
    }

    public T Get<T>() where T : notnull => _sp.GetRequiredService<T>();
    public IAuthService Auth => Get<IAuthService>();
    public IUserService Users => Get<IUserService>();
    public IFriendService Friends => Get<IFriendService>();
    public IConversationService Conversations => Get<IConversationService>();
    public IMessageService Messages => Get<IMessageService>();
    public IFileStorageService Files => Get<IFileStorageService>();

    public User AddUser(string username, string? displayName = null)
    {
        var u = User.Create(username, displayName ?? username, "hash:Passw0rd!", Clock.UtcNow);
        Db.Users.Add(u);
        return u;
    }

    /// <summary>Creates an Accepted friendship plus the direct conversation (what AcceptAsync would do).</summary>
    public Conversation MakeFriends(User a, User b)
    {
        var f = Friendship.Create(a, b, Clock.UtcNow);
        f.Accept(Clock.UtcNow);
        Db.Friendships.Add(f);
        var c = Conversation.CreateDirect(a.Id, b.Id, Clock.UtcNow);
        Db.Conversations.Add(c);
        return c;
    }

    public void Block(User blocker, User blocked) => Db.Blocks.Add(UserBlock.Create(blocker.Id, blocked.Id, Clock.UtcNow));

    /// <summary>Group with the owner and members (all must already be friends of the owner for service calls).</summary>
    public Conversation AddGroup(User owner, params User[] members)
    {
        var c = Conversation.CreateGroup("Team", owner.Id, members.Select(m => m.Id), Clock.UtcNow);
        Db.Conversations.Add(c);
        return c;
    }

    public async Task<Domain.Dtos.MessageDto> SendText(User from, Guid convId, string text)
        => await Messages.SendAsync(from.Id, convId, new Domain.Dtos.SendMessageDto(MessageType.Text, text, null, null, null));

    public static void SetRole(Conversation c, Guid userId, MemberRole role) => c.GetMember(userId)!.SetRole(role);

    public static void SetJoinedAt(Conversation c, Guid userId, DateTime at)
        => typeof(ConversationMember).GetProperty(nameof(ConversationMember.JoinedAt), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(c.GetMember(userId), at);
}

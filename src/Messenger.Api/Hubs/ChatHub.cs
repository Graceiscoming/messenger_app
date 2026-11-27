using Messenger.Api.Infrastructure;
using Messenger.Api.Realtime;
using Messenger.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Messenger.Api.Hubs;

[Authorize]
public sealed class ChatHub(
    PresenceTracker presence,
    IConversationService conversations,
    IFriendService friends,
    IUserService users,
    ILogger<ChatHub> logger) : Hub
{
    private Guid CurrentUserId => Context.User!.GetUserId();

    public override async Task OnConnectedAsync()
    {
        var userId = CurrentUserId;
        var ct = Context.ConnectionAborted;

        var first = presence.Connect(userId, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.User(userId), ct);
        foreach (var convId in await conversations.ListConversationIdsAsync(userId, ct))
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Conversation(convId), ct);

        if (first) await BroadcastPresenceAsync(userId, true, ct);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = CurrentUserId;
        if (presence.Disconnect(userId, Context.ConnectionId))
        {
            try
            {
                await users.MarkSeenAsync(userId);
                await BroadcastPresenceAsync(userId, false, CancellationToken.None);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Failed to finalize disconnect for user {UserId}", userId);
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Client -> server. Silently ignored unless the caller is an active member.</summary>
    public async Task Typing(Guid conversationId)
    {
        var userId = CurrentUserId;
        if (!await conversations.IsMemberAsync(userId, conversationId, Context.ConnectionAborted)) return;
        await Clients.OthersInGroup(HubGroups.Conversation(conversationId))
            .SendAsync(HubEvents.UserTyping, new { conversationId, userId });
    }

    private async Task BroadcastPresenceAsync(Guid userId, bool isOnline, CancellationToken ct)
    {
        var friendIds = await friends.GetFriendIdsAsync(userId, ct);
        if (friendIds.Count == 0) return;
        await Clients.Groups(friendIds.Select(HubGroups.User).ToList())
            .SendAsync(HubEvents.PresenceChanged, new { userId, isOnline }, ct);
    }
}

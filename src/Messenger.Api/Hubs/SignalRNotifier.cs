using Messenger.Api.Realtime;
using Messenger.Domain.Abstractions;
using Messenger.Domain.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace Messenger.Api.Hubs;

public static class HubGroups
{
    public static string User(Guid id) => $"user:{id}";
    public static string Conversation(Guid id) => $"conv:{id}";
}

public static class HubEvents
{
    public const string MessageReceived = "MessageReceived";
    public const string MessageUnsent = "MessageUnsent";
    public const string MessageRead = "MessageRead";
    public const string ConversationChanged = "ConversationChanged";
    public const string FriendRequestReceived = "FriendRequestReceived";
    public const string FriendRequestAccepted = "FriendRequestAccepted";
    public const string FriendRemoved = "FriendRemoved";
    public const string PresenceChanged = "PresenceChanged";
    public const string UserTyping = "UserTyping";
}

public sealed class SignalRNotifier(IHubContext<ChatHub> hub, PresenceTracker presence) : IRealtimeNotifier
{
    public Task NotifyMessageReceived(IEnumerable<Guid> userIds, MessageDto message, CancellationToken ct = default)
    {
        var groups = userIds.Distinct().Select(HubGroups.User).ToList();
        return groups.Count == 0
            ? Task.CompletedTask
            : hub.Clients.Groups(groups).SendAsync(HubEvents.MessageReceived, message, ct);
    }

    public Task NotifyMessageUnsent(Guid conversationId, long messageId, CancellationToken ct = default)
        => hub.Clients.Group(HubGroups.Conversation(conversationId))
            .SendAsync(HubEvents.MessageUnsent, new { conversationId, messageId }, ct);

    public Task NotifyFriendEvent(Guid userId, string type, FriendshipDto friendship, CancellationToken ct = default)
    {
        var evt = type == FriendEventTypes.Accepted ? HubEvents.FriendRequestAccepted : HubEvents.FriendRequestReceived;
        return hub.Clients.Group(HubGroups.User(userId)).SendAsync(evt, friendship, ct);
    }

    public Task NotifyFriendRemoved(Guid recipientId, Guid friendId, CancellationToken ct = default)
        => hub.Clients.Group(HubGroups.User(recipientId)).SendAsync(HubEvents.FriendRemoved, new { userId = friendId }, ct);

    public Task NotifyConversationChanged(Guid userId, ConversationDto conversation, CancellationToken ct = default)
        => hub.Clients.Group(HubGroups.User(userId)).SendAsync(HubEvents.ConversationChanged, conversation, ct);

    public async Task NotifyMembershipChanged(Guid conversationId, IReadOnlyCollection<Guid> addedUserIds,
        IReadOnlyCollection<Guid> removedUserIds, CancellationToken ct = default)
    {
        var group = HubGroups.Conversation(conversationId);
        foreach (var userId in addedUserIds)
            foreach (var connectionId in presence.GetConnections(userId))
                await hub.Groups.AddToGroupAsync(connectionId, group, ct);
        foreach (var userId in removedUserIds)
            foreach (var connectionId in presence.GetConnections(userId))
                await hub.Groups.RemoveFromGroupAsync(connectionId, group, ct);
    }

    public Task NotifyRead(Guid conversationId, Guid userId, long messageId, CancellationToken ct = default)
        => hub.Clients.Group(HubGroups.Conversation(conversationId))
            .SendAsync(HubEvents.MessageRead, new { conversationId, userId, messageId }, ct);
}

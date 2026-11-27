using Messenger.Domain.Abstractions;
using Messenger.Domain.Dtos;
using Messenger.Domain.Entities;
using Messenger.Domain.Enums;
using Messenger.Domain.Exceptions;

namespace Messenger.Domain.Services;

internal sealed class FriendService(
    IUserRepository users,
    IFriendshipRepository friendships,
    IUserBlockRepository blocks,
    IConversationRepository conversations,
    IPresenceTracker presence,
    IRealtimeNotifier notifier,
    IUnitOfWork uow,
    IClock clock,
    ConversationDtoBuilder conversationDtos) : IFriendService
{
    public async Task<FriendshipDto> SendRequestByUsernameAsync(Guid me, string username, CancellationToken ct = default)
    {
        var target = await users.FindByNormalizedUsernameAsync(User.Normalize(username ?? ""), ct)
                     ?? throw new NotFoundException("User not found.");
        return await SendRequestAsync(me, target.Id, ct);
    }

    public async Task<FriendshipDto> SendRequestAsync(Guid me, Guid target, CancellationToken ct = default)
    {
        if (me == target) throw new ValidationException("userId", "You cannot send a friend request to yourself.");

        var requester = await users.FindByIdAsync(me, ct) ?? throw new NotFoundException("User not found.");
        var addressee = await users.FindByIdAsync(target, ct) ?? throw new NotFoundException("User not found.");

        if (await blocks.ExistsBetweenAsync(me, target, ct))
            throw new ForbiddenException("You cannot send a friend request to this user.");

        var existing = await friendships.FindBetweenAsync(me, target, ct); // BR-3: one record per pair
        if (existing is not null)
        {
            if (existing.Status == FriendshipStatus.Accepted)
                throw new ConflictException("You are already friends.");
            if (existing.RequesterId == target)
            {
                // UC-03 alt: the other side already asked -> accept automatically.
                return await AcceptCoreAsync(existing, ct);
            }
            throw new ConflictException("A friend request is already pending.");
        }

        var friendship = Friendship.Create(requester, addressee, clock.UtcNow);
        await friendships.AddAsync(friendship, ct);
        await uow.SaveChangesAsync(ct);

        var dto = friendship.ToDto();
        await notifier.NotifyFriendEvent(target, FriendEventTypes.Received, dto, ct);
        return dto;
    }

    public async Task AcceptAsync(Guid me, Guid friendshipId, CancellationToken ct = default)
    {
        var friendship = await friendships.FindByIdAsync(friendshipId, ct)
                         ?? throw new NotFoundException("Friend request not found.");
        if (friendship.AddresseeId != me)
            throw new ForbiddenException("Only the recipient can accept a friend request.");
        if (friendship.Status != FriendshipStatus.Pending)
            throw new ConflictException("Friend request is not pending.");
        if (await blocks.ExistsBetweenAsync(friendship.RequesterId, friendship.AddresseeId, ct))
            throw new ForbiddenException("You cannot accept a request from this user.");

        await AcceptCoreAsync(friendship, ct);
    }

    public async Task DeclineAsync(Guid me, Guid friendshipId, CancellationToken ct = default)
    {
        var friendship = await friendships.FindByIdAsync(friendshipId, ct)
                         ?? throw new NotFoundException("Friend request not found.");
        if (friendship.AddresseeId != me)
            throw new ForbiddenException("Only the recipient can decline a friend request.");
        if (friendship.Status != FriendshipStatus.Pending)
            throw new ConflictException("Friend request is not pending.");

        friendships.Remove(friendship);
        await uow.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(Guid me, Guid friendshipId, CancellationToken ct = default)
    {
        var friendship = await friendships.FindByIdAsync(friendshipId, ct)
                         ?? throw new NotFoundException("Friend request not found.");
        if (friendship.RequesterId != me)
            throw new ForbiddenException("Only the sender can cancel a friend request.");
        if (friendship.Status != FriendshipStatus.Pending)
            throw new ConflictException("Friend request is not pending.");

        friendships.Remove(friendship);
        await uow.SaveChangesAsync(ct);
    }

    public async Task RemoveFriendAsync(Guid me, Guid friendId, CancellationToken ct = default)
    {
        var friendship = await friendships.FindBetweenAsync(me, friendId, ct);
        if (friendship is null || friendship.Status != FriendshipStatus.Accepted)
            throw new NotFoundException("Friend not found.");

        // BR-5: friendship goes away, the direct conversation and its history stay (now read-only).
        friendships.Remove(friendship);
        await uow.SaveChangesAsync(ct);

        await notifier.NotifyFriendRemoved(friendId, me, ct);
        var direct = await conversations.FindDirectAsync(Conversation.BuildDirectKey(me, friendId), ct);
        if (direct is not null)
            foreach (var uid in new[] { me, friendId })
            {
                var dto = await conversationDtos.BuildOneAsync(uid, direct.Id, ct);
                if (dto is not null) await notifier.NotifyConversationChanged(uid, dto, ct);
            }
    }

    public async Task<IReadOnlyList<FriendDto>> ListFriendsAsync(Guid me, CancellationToken ct = default)
    {
        var accepted = await friendships.ListAcceptedAsync(me, ct);
        var keys = accepted.Select(f => Conversation.BuildDirectKey(me, f.OtherUserId(me))).ToList();
        var convIds = keys.Count == 0
            ? new Dictionary<string, Guid>()
            : (await conversations.GetDirectConversationIdsAsync(keys, ct)).ToDictionary(k => k.Key, k => k.Value);

        return accepted
            .Select(f =>
            {
                var other = f.OtherUser(me) ?? throw new InvalidOperationException("Friendship users not loaded");
                Guid? convId = convIds.TryGetValue(Conversation.BuildDirectKey(me, other.Id), out var id) ? id : null;
                return new FriendDto(other.ToDto(), presence.IsOnline(other.Id), convId);
            })
            .OrderBy(f => f.User.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<FriendRequestsDto> ListRequestsAsync(Guid me, CancellationToken ct = default)
    {
        var pending = await friendships.ListPendingAsync(me, ct);
        return new FriendRequestsDto(
            pending.Where(f => f.AddresseeId == me).OrderByDescending(f => f.CreatedAt).Select(f => f.ToDto()).ToList(),
            pending.Where(f => f.RequesterId == me).OrderByDescending(f => f.CreatedAt).Select(f => f.ToDto()).ToList());
    }

    public async Task<bool> AreFriendsAsync(Guid a, Guid b, CancellationToken ct = default)
    {
        if (a == b) return false;
        var f = await friendships.FindBetweenAsync(a, b, ct);
        return f is { Status: FriendshipStatus.Accepted };
    }

    public async Task<IReadOnlyList<Guid>> GetFriendIdsAsync(Guid me, CancellationToken ct = default)
        => (await friendships.ListAcceptedAsync(me, ct)).Select(f => f.OtherUserId(me)).ToList();

    private async Task<FriendshipDto> AcceptCoreAsync(Friendship friendship, CancellationToken ct)
    {
        // The accepting side is the addressee; the requester is told.
        friendship.Accept(clock.UtcNow);
        var (conversation, _) = await DirectConversations.EnsureAsync(
            conversations, friendship.RequesterId, friendship.AddresseeId, clock.UtcNow, ct);
        await uow.SaveChangesAsync(ct);

        var dto = friendship.ToDto();
        await notifier.NotifyFriendEvent(friendship.RequesterId, FriendEventTypes.Accepted, dto, ct);

        var both = new[] { friendship.RequesterId, friendship.AddresseeId };
        await notifier.NotifyMembershipChanged(conversation.Id, both, Array.Empty<Guid>(), ct);
        foreach (var uid in both)
        {
            var convDto = await conversationDtos.BuildOneAsync(uid, conversation.Id, ct);
            if (convDto is not null) await notifier.NotifyConversationChanged(uid, convDto, ct);
        }
        return dto;
    }
}

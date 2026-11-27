using Messenger.Domain.Enums;
using Messenger.Domain.Exceptions;

namespace Messenger.Domain.Entities;

public sealed class Friendship
{
    private Friendship() { }

    public Guid Id { get; private set; }
    public Guid RequesterId { get; private set; }
    public Guid AddresseeId { get; private set; }
    public FriendshipStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RespondedAt { get; private set; }

    /// <summary>Navigations; repositories must load both users.</summary>
    public User? Requester { get; private set; }
    public User? Addressee { get; private set; }

    public static Friendship Create(User requester, User addressee, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        RequesterId = requester.Id,
        AddresseeId = addressee.Id,
        Status = FriendshipStatus.Pending,
        CreatedAt = now,
        Requester = requester,
        Addressee = addressee,
    };

    public void Accept(DateTime now)
    {
        if (Status != FriendshipStatus.Pending)
            throw new ConflictException("Friend request is not pending.");
        Status = FriendshipStatus.Accepted;
        RespondedAt = now;
    }

    public bool Involves(Guid userId) => RequesterId == userId || AddresseeId == userId;
    public Guid OtherUserId(Guid userId) => RequesterId == userId ? AddresseeId : RequesterId;
    public User? OtherUser(Guid userId) => RequesterId == userId ? Addressee : Requester;

    /// <summary>For repositories (and tests) that populate navigations manually.</summary>
    public void AttachUsers(User requester, User addressee)
    {
        Requester = requester;
        Addressee = addressee;
    }
}

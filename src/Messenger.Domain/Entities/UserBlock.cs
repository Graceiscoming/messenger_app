namespace Messenger.Domain.Entities;

public sealed class UserBlock
{
    private UserBlock() { }

    public Guid BlockerId { get; private set; }
    public Guid BlockedId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static UserBlock Create(Guid blockerId, Guid blockedId, DateTime now) => new()
    {
        BlockerId = blockerId,
        BlockedId = blockedId,
        CreatedAt = now,
    };
}

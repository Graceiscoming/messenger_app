using Messenger.Domain.Abstractions;
using Messenger.Domain.Dtos;
using Messenger.Domain.Entities;
using Messenger.Domain.Enums;
using Messenger.Domain.Exceptions;

namespace Messenger.Domain.Services;

internal sealed class UserService(
    IUserRepository users,
    IFriendshipRepository friendships,
    IUserBlockRepository blocks,
    IConversationRepository conversations,
    IAttachmentRepository attachments,
    IFileStorageService files,
    IRealtimeNotifier notifier,
    IUnitOfWork uow,
    IClock clock,
    ConversationDtoBuilder conversationDtos) : IUserService
{
    private const int SearchLimit = 20;
    private const int MaxStatusLength = 100;

    public async Task<UserDto> GetProfileAsync(Guid id, CancellationToken ct = default)
        => (await users.FindByIdAsync(id, ct) ?? throw new NotFoundException("User not found.")).ToDto();

    public async Task<UserDto> UpdateProfileAsync(Guid id, UpdateProfileDto dto, CancellationToken ct = default)
    {
        Rules.DisplayName(dto.DisplayName);
        if (dto.StatusMessage is { Length: > MaxStatusLength })
            throw new ValidationException("statusMessage", $"Status message must be at most {MaxStatusLength} characters.");

        var user = await users.FindByIdAsync(id, ct) ?? throw new NotFoundException("User not found.");
        user.UpdateProfile(dto.DisplayName, dto.StatusMessage);
        await uow.SaveChangesAsync(ct);
        return user.ToDto();
    }

    public async Task<UserDto> SetAvatarAsync(Guid me, Stream content, string fileName, string contentType,
        CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(me, ct) ?? throw new NotFoundException("User not found.");
        var saved = await files.SaveImageAsync(me, content, fileName, contentType, ct);
        var old = user.AvatarAttachmentId;
        user.SetAvatar(saved.AttachmentId);
        await uow.SaveChangesAsync(ct);
        // Attachments are single-use: delete the old avatar only if nothing else references it.
        if (old is not null && !await attachments.IsReferencedAsync(old.Value, ct))
            await files.DeleteAsync(old.Value, ct);
        return user.ToDto();
    }

    public async Task<IReadOnlyList<UserDto>> SearchAsync(Guid me, string username, CancellationToken ct = default)
    {
        var q = User.Normalize(username ?? "");
        if (q.Length == 0) return Array.Empty<UserDto>();

        var blockers = (await blocks.ListBlockerIdsAsync(me, ct)).ToHashSet();
        var found = await users.SearchByUsernamePrefixAsync(q, SearchLimit + blockers.Count + 1, ct);
        return found.Where(u => u.Id != me && !blockers.Contains(u.Id))
            .Take(SearchLimit).Select(u => u.ToDto()).ToList();
    }

    public async Task BlockAsync(Guid me, Guid target, CancellationToken ct = default)
    {
        if (me == target) throw new ValidationException("userId", "You cannot block yourself.");
        if (await users.FindByIdAsync(target, ct) is null) throw new NotFoundException("User not found.");
        if (await blocks.FindAsync(me, target, ct) is not null) return; // idempotent

        await blocks.AddAsync(UserBlock.Create(me, target, clock.UtcNow), ct);

        // UC-10: blocking ends an existing friendship / pending request.
        var friendship = await friendships.FindBetweenAsync(me, target, ct);
        var wasFriend = friendship is { Status: FriendshipStatus.Accepted };
        if (friendship is not null) friendships.Remove(friendship);
        await uow.SaveChangesAsync(ct);

        if (wasFriend)
        {
            await notifier.NotifyFriendRemoved(target, me, ct);
            var dtoConv = await conversations.FindDirectAsync(Conversation.BuildDirectKey(me, target), ct);
            if (dtoConv is not null)
                foreach (var uid in new[] { me, target })
                {
                    var dto = await conversationDtos.BuildOneAsync(uid, dtoConv.Id, ct);
                    if (dto is not null) await notifier.NotifyConversationChanged(uid, dto, ct);
                }
        }
    }

    public async Task UnblockAsync(Guid me, Guid target, CancellationToken ct = default)
    {
        var block = await blocks.FindAsync(me, target, ct);
        if (block is null) return; // idempotent
        blocks.Remove(block);
        await uow.SaveChangesAsync(ct);
    }

    public async Task MarkSeenAsync(Guid me, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(me, ct);
        if (user is null) return;
        user.SetLastSeen(clock.UtcNow);
        await uow.SaveChangesAsync(ct);
    }
}

using Messenger.Domain.Abstractions;
using Messenger.Domain.Dtos;
using Messenger.Domain.Entities;
using Messenger.Domain.Enums;
using Messenger.Domain.Exceptions;

namespace Messenger.Domain.Services;

internal sealed class ConversationService(
    IConversationRepository conversations,
    IMessageRepository messages,
    IAttachmentRepository attachments,
    IUserRepository users,
    IUserBlockRepository blocks,
    IFriendService friends,
    IFileStorageService files,
    IRealtimeNotifier notifier,
    IUnitOfWork uow,
    IClock clock,
    ConversationDtoBuilder dtos) : IConversationService
{
    public async Task<IReadOnlyList<ConversationDto>> ListAsync(Guid me, CancellationToken ct = default)
        => await dtos.BuildManyAsync(me, await conversations.ListForUserAsync(me, ct), ct);

    public async Task<ConversationDto> GetAsync(Guid me, Guid id, CancellationToken ct = default)
    {
        var dto = await dtos.BuildOneAsync(me, id, ct);
        if (dto is not null) return dto;
        _ = await conversations.FindByIdWithMembersAsync(id, ct) ?? throw new NotFoundException("Conversation not found.");
        throw new ForbiddenException("You are not a member of this conversation.");
    }

    public async Task<ConversationDto> GetOrCreateDirectAsync(Guid me, Guid friendId, CancellationToken ct = default)
    {
        if (me == friendId) throw new ValidationException("friendId", "Cannot open a conversation with yourself.");

        var existing = await conversations.FindDirectAsync(Conversation.BuildDirectKey(me, friendId), ct);
        if (existing is not null)
        {
            if (!existing.IsMember(me)) throw new ForbiddenException("You are not a member of this conversation.");
            return await GetAsync(me, existing.Id, ct); // history stays reachable even if no longer friends (BR-5)
        }

        if (await users.FindByIdAsync(friendId, ct) is null) throw new NotFoundException("User not found.");
        await RequireFriendAsync(me, friendId, ct);

        var (conversation, _) = await DirectConversations.EnsureAsync(conversations, me, friendId, clock.UtcNow, ct);
        await uow.SaveChangesAsync(ct);

        var both = new[] { me, friendId };
        await notifier.NotifyMembershipChanged(conversation.Id, both, Array.Empty<Guid>(), ct);
        await NotifyConversationChangedAsync(both, conversation.Id, ct);
        return await GetAsync(me, conversation.Id, ct);
    }

    public async Task<ConversationDto> CreateGroupAsync(Guid me, CreateGroupDto dto, CancellationToken ct = default)
    {
        Rules.GroupName(dto.Name);
        var memberIds = (dto.MemberIds ?? Array.Empty<Guid>()).Where(i => i != me).Distinct().ToList();
        if (memberIds.Count < 1)       // BR-6: owner + at least one more member
            throw new ValidationException("memberIds", "A group needs at least 2 members.");

        var owner = await users.FindByIdAsync(me, ct) ?? throw new NotFoundException("User not found.");
        var found = await users.FindByIdsAsync(memberIds, ct);
        if (found.Count != memberIds.Count) throw new NotFoundException("One or more users were not found.");
        foreach (var id in memberIds) await RequireFriendAsync(me, id, ct);   // BR-6

        var conversation = Conversation.CreateGroup(dto.Name, me, memberIds, clock.UtcNow);
        await conversations.AddAsync(conversation, ct);
        await uow.SaveChangesAsync(ct);

        var system = await PostSystemAsync(conversation, owner, SystemText.GroupCreated(owner.DisplayName), ct);
        var all = conversation.ActiveMembers.Select(m => m.UserId).ToList();
        await FinishAsync(conversation, all, Array.Empty<Guid>(), new[] { system }, ct);
        return await GetAsync(me, conversation.Id, ct);
    }

    public async Task<ConversationDto> UpdateGroupAsync(Guid me, Guid id, UpdateGroupDto dto, CancellationToken ct = default)
    {
        var (conversation, actor) = await LoadGroupForManagerAsync(me, id, ct);
        if (dto.Name is null) throw new ValidationException("name", "Nothing to update.");
        Rules.GroupName(dto.Name);

        if (conversation.Name != dto.Name.Trim())
        {
            conversation.Rename(dto.Name);
            await uow.SaveChangesAsync(ct);
            var system = await PostSystemAsync(conversation, actor, SystemText.Renamed(actor.DisplayName, conversation.Name!), ct);
            await FinishAsync(conversation, Array.Empty<Guid>(), Array.Empty<Guid>(), new[] { system }, ct);
        }
        return await GetAsync(me, id, ct);
    }

    public async Task<ConversationDto> SetGroupAvatarAsync(Guid me, Guid id, Stream content, string fileName,
        string contentType, CancellationToken ct = default)
    {
        var (conversation, actor) = await LoadGroupForManagerAsync(me, id, ct);
        var saved = await files.SaveImageAsync(me, content, fileName, contentType, ct);
        var old = conversation.AvatarAttachmentId;
        conversation.SetAvatar(saved.AttachmentId);
        await uow.SaveChangesAsync(ct);
        // Attachments are single-use: delete the old avatar only if nothing else references it.
        if (old is not null && !await attachments.IsReferencedAsync(old.Value, ct))
            await files.DeleteAsync(old.Value, ct);

        var system = await PostSystemAsync(conversation, actor, SystemText.AvatarChanged(actor.DisplayName), ct);
        await FinishAsync(conversation, Array.Empty<Guid>(), Array.Empty<Guid>(), new[] { system }, ct);
        return await GetAsync(me, id, ct);
    }

    public async Task<ConversationDto> AddMembersAsync(Guid me, Guid id, IReadOnlyList<Guid> userIds, CancellationToken ct = default)
    {
        var (conversation, actor) = await LoadGroupForManagerAsync(me, id, ct);
        var targets = (userIds ?? Array.Empty<Guid>()).Where(i => i != me).Distinct().ToList();
        if (targets.Count == 0) throw new ValidationException("userIds", "No users to add.");

        var found = await users.FindByIdsAsync(targets, ct);
        if (found.Count != targets.Count) throw new NotFoundException("One or more users were not found.");
        foreach (var uid in targets) await RequireFriendAsync(me, uid, ct);   // BR-6: only the adder's friends

        var toAdd = targets.Where(t => !conversation.IsMember(t)).ToList();
        if (toAdd.Count == 0) throw new ConflictException("All users are already members.");

        var now = clock.UtcNow;
        foreach (var uid in toAdd) conversation.AddMember(uid, MemberRole.Member, now);
        await uow.SaveChangesAsync(ct);

        var names = found.Where(u => toAdd.Contains(u.Id)).Select(u => u.DisplayName).ToList();
        var system = await PostSystemAsync(conversation, actor, SystemText.MembersAdded(actor.DisplayName, names), ct);
        await FinishAsync(conversation, toAdd, Array.Empty<Guid>(), new[] { system }, ct);
        return await GetAsync(me, id, ct);
    }

    public async Task RemoveMemberAsync(Guid me, Guid id, Guid target, CancellationToken ct = default)
    {
        var (conversation, actor) = await LoadGroupForManagerAsync(me, id, ct);
        if (target == me) throw new ValidationException("userId", "Use the leave endpoint to leave a group.");

        var targetMember = conversation.GetMember(target);
        if (targetMember is not { IsActive: true }) throw new NotFoundException("Member not found.");
        var actorMember = conversation.GetMember(me)!;
        // Owner can remove anyone; Admin can only remove plain members (BR-7).
        if (targetMember.Role == MemberRole.Owner || (targetMember.Role == MemberRole.Admin && actorMember.Role != MemberRole.Owner))
            throw new ForbiddenException("You cannot remove this member.");

        var targetName = targetMember.User?.DisplayName
                         ?? (await users.FindByIdAsync(target, ct))?.DisplayName ?? "";
        conversation.RemoveMember(target, clock.UtcNow);
        await uow.SaveChangesAsync(ct);

        var system = await PostSystemAsync(conversation, actor, SystemText.MemberRemoved(actor.DisplayName, targetName), ct);
        await FinishAsync(conversation, Array.Empty<Guid>(), new[] { target }, new[] { system }, ct);
    }

    public async Task LeaveAsync(Guid me, Guid id, CancellationToken ct = default)
    {
        var conversation = await conversations.FindByIdWithMembersAsync(id, ct)
                           ?? throw new NotFoundException("Conversation not found.");
        if (!conversation.IsGroup) throw new ValidationException("id", "You can only leave group conversations.");
        var member = conversation.GetMember(me);
        if (member is not { IsActive: true }) throw new ForbiddenException("You are not a member of this conversation.");

        var actor = await users.FindByIdAsync(me, ct) ?? throw new NotFoundException("User not found.");
        var now = clock.UtcNow;

        ConversationMember? successor = null;
        if (member.Role == MemberRole.Owner)
        {
            // BR-7: hand ownership to the longest-standing remaining member.
            successor = conversation.ActiveMembers.Where(m => m.UserId != me).OrderBy(m => m.JoinedAt).FirstOrDefault();
            successor?.SetRole(MemberRole.Owner);
        }
        conversation.RemoveMember(me, now);
        await uow.SaveChangesAsync(ct);

        var systems = new List<MessageDto> { await PostSystemAsync(conversation, actor, SystemText.MemberLeft(actor.DisplayName), ct) };
        if (successor is not null)
        {
            var newOwnerName = successor.User?.DisplayName ?? (await users.FindByIdAsync(successor.UserId, ct))?.DisplayName ?? "";
            systems.Add(await PostSystemAsync(conversation, actor, SystemText.OwnerTransferred(newOwnerName), ct));
        }
        await FinishAsync(conversation, Array.Empty<Guid>(), new[] { me }, systems, ct);
    }

    public async Task SetRoleAsync(Guid me, Guid id, Guid target, MemberRole role, CancellationToken ct = default)
    {
        var conversation = await conversations.FindByIdWithMembersAsync(id, ct)
                           ?? throw new NotFoundException("Conversation not found.");
        if (!Enum.IsDefined(role)) throw new ValidationException("role", "Unknown role.");
        if (!conversation.IsGroup) throw new ValidationException("id", "Roles only exist in group conversations.");
        var actorMember = conversation.GetMember(me);
        if (actorMember is not { IsActive: true }) throw new ForbiddenException("You are not a member of this conversation.");
        if (actorMember.Role != MemberRole.Owner) throw new ForbiddenException("Only the owner can change roles.");  // BR-7
        if (target == me) throw new ValidationException("userId", "You cannot change your own role.");
        var targetMember = conversation.GetMember(target);
        if (targetMember is not { IsActive: true }) throw new NotFoundException("Member not found.");
        if (targetMember.Role == role) return;

        var actor = await users.FindByIdAsync(me, ct) ?? throw new NotFoundException("User not found.");
        var targetName = targetMember.User?.DisplayName ?? (await users.FindByIdAsync(target, ct))?.DisplayName ?? "";

        string text;
        if (role == MemberRole.Owner)
        {
            // Transfer of ownership: previous owner becomes Admin.
            targetMember.SetRole(MemberRole.Owner);
            actorMember.SetRole(MemberRole.Admin);
            text = SystemText.OwnerTransferred(targetName);
        }
        else
        {
            targetMember.SetRole(role);
            text = SystemText.RoleChanged(actor.DisplayName, targetName, role);
        }
        await uow.SaveChangesAsync(ct);

        var system = await PostSystemAsync(conversation, actor, text, ct);
        await FinishAsync(conversation, Array.Empty<Guid>(), Array.Empty<Guid>(), new[] { system }, ct);
    }

    public async Task MarkReadAsync(Guid me, Guid id, long messageId, CancellationToken ct = default)
    {
        var conversation = await conversations.FindByIdWithMembersAsync(id, ct)
                           ?? throw new NotFoundException("Conversation not found.");
        var member = conversation.GetMember(me);
        if (member is not { IsActive: true }) throw new ForbiddenException("You are not a member of this conversation.");

        var message = await messages.FindByIdAsync(messageId, ct);
        if (message is null || message.ConversationId != id) throw new NotFoundException("Message not found.");

        if (!member.MarkRead(messageId)) return;   // BR-11, never moves backwards
        await uow.SaveChangesAsync(ct);
        await notifier.NotifyRead(id, me, member.LastReadMessageId!.Value, ct);
    }

    public Task<IReadOnlyList<Guid>> ListConversationIdsAsync(Guid me, CancellationToken ct = default)
        => conversations.ListConversationIdsForUserAsync(me, ct);

    public Task<bool> IsMemberAsync(Guid me, Guid id, CancellationToken ct = default)
        => conversations.IsActiveMemberAsync(id, me, ct);

    // ---------------------------------------------------------------- helpers

    private async Task RequireFriendAsync(Guid me, Guid other, CancellationToken ct)
    {
        if (!await friends.AreFriendsAsync(me, other, ct) || await blocks.ExistsBetweenAsync(me, other, ct))
            throw new ForbiddenException("You can only do this with your friends.");
    }

    private async Task<(Conversation Conversation, User Actor)> LoadGroupForManagerAsync(Guid me, Guid id, CancellationToken ct)
    {
        var conversation = await conversations.FindByIdWithMembersAsync(id, ct)
                           ?? throw new NotFoundException("Conversation not found.");
        if (!conversation.IsGroup) throw new ValidationException("id", "This operation only applies to group conversations.");
        var member = conversation.GetMember(me);
        if (member is not { IsActive: true }) throw new ForbiddenException("You are not a member of this conversation.");
        if (!member.CanManageMembers()) throw new ForbiddenException("Only owners and admins can manage the group.");  // BR-7
        var actor = await users.FindByIdAsync(me, ct) ?? throw new NotFoundException("User not found.");
        return (conversation, actor);
    }

    /// <summary>Adds a System message (UC-09) and updates the conversation's last message. Does not notify.</summary>
    private async Task<MessageDto> PostSystemAsync(Conversation conversation, User actor, string content, CancellationToken ct)
    {
        var message = Message.CreateSystem(conversation.Id, actor.Id, content, clock.UtcNow);
        message.AttachNavigation(actor, null, null);
        await uow.ExecuteInTransactionAsync(async token =>
        {
            await messages.AddAsync(message, token);
            await uow.SaveChangesAsync(token);              // assigns message.Id
            conversation.RegisterMessage(message.Id, message.CreatedAt);
            await uow.SaveChangesAsync(token);
        }, ct);
        return message.ToDto();
    }

    /// <summary>Notifies after save: membership (hub groups), personalised conversation DTOs, system messages.</summary>
    private async Task FinishAsync(Conversation conversation, IReadOnlyCollection<Guid> added,
        IReadOnlyCollection<Guid> removed, IReadOnlyList<MessageDto> systemMessages, CancellationToken ct)
    {
        if (added.Count > 0 || removed.Count > 0)
            await notifier.NotifyMembershipChanged(conversation.Id, added, removed, ct);

        var active = conversation.ActiveMembers.Select(m => m.UserId).ToList();
        await NotifyConversationChangedAsync(active, conversation.Id, ct);

        // Removed/left users must learn they are out: send them a DTO whose members exclude them.
        if (removed.Count > 0 && active.Count > 0)
        {
            var summary = await conversations.GetSummaryAsync(active[0], conversation.Id, ct);
            if (summary is not null)
            {
                var forRemoved = summary with { UnreadCount = 0 };
                foreach (var uid in removed)
                {
                    var dto = (await dtos.BuildManyAsync(uid, new[] { forRemoved }, ct))[0];
                    await notifier.NotifyConversationChanged(uid, dto, ct);
                }
            }
        }

        var recipients = active.Concat(removed).Distinct().ToList();
        foreach (var message in systemMessages)
            await notifier.NotifyMessageReceived(recipients, message, ct);
    }

    private async Task NotifyConversationChangedAsync(IEnumerable<Guid> userIds, Guid conversationId, CancellationToken ct)
    {
        foreach (var uid in userIds)
        {
            var dto = await dtos.BuildOneAsync(uid, conversationId, ct);
            if (dto is not null) await notifier.NotifyConversationChanged(uid, dto, ct);
        }
    }
}

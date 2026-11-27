using Messenger.Domain.Abstractions;

namespace Messenger.Api.Realtime;

/// <summary>In-memory, thread-safe map userId -> live SignalR connection ids. Online = at least one connection.</summary>
public sealed class PresenceTracker : IPresenceTracker
{
    private readonly Dictionary<Guid, HashSet<string>> _connections = new();
    private readonly object _gate = new();

    /// <summary>Returns true when this is the user's first live connection.</summary>
    public bool Connect(Guid userId, string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(userId, out var set))
            {
                _connections[userId] = set = new HashSet<string>();
            }
            var first = set.Count == 0;
            set.Add(connectionId);
            return first;
        }
    }

    /// <summary>Returns true when the removed connection was the user's last one.</summary>
    public bool Disconnect(Guid userId, string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(userId, out var set)) return false;
            if (!set.Remove(connectionId)) return false;
            if (set.Count > 0) return false;
            _connections.Remove(userId);
            return true;
        }
    }

    public IReadOnlyList<string> GetConnections(Guid userId)
    {
        lock (_gate)
        {
            return _connections.TryGetValue(userId, out var set) ? set.ToArray() : Array.Empty<string>();
        }
    }

    public bool IsOnline(Guid userId)
    {
        lock (_gate) return _connections.ContainsKey(userId);
    }

    public IReadOnlyCollection<Guid> GetOnlineUsers(IEnumerable<Guid> userIds)
    {
        lock (_gate) return userIds.Where(_connections.ContainsKey).Distinct().ToArray();
    }
}

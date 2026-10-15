using System.Collections.Concurrent;

namespace ChatApp.Api.Services;

/// <summary>
/// Tracks which users are currently online. Maps user id -> active connection ids,
/// so a user with several tabs/devices stays "online" until the last one disconnects.
/// Single-server implementation; see README for how this scales with a Redis backplane.
/// </summary>
public interface IPresenceTracker
{
    /// <returns>True when the user transitioned from offline to online.</returns>
    bool UserConnected(string userId, string connectionId);

    /// <returns>True when the user transitioned from online to fully offline.</returns>
    bool UserDisconnected(string userId, string connectionId);

    bool IsOnline(string userId);
    IReadOnlyCollection<string> GetOnlineUsers();
}

public class PresenceTracker : IPresenceTracker
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _connections = new();

    public bool UserConnected(string userId, string connectionId)
    {
        var set = _connections.GetOrAdd(userId, _ => new HashSet<string>());
        lock (set)
        {
            var wasEmpty = set.Count == 0;
            set.Add(connectionId);
            return wasEmpty;
        }
    }

    public bool UserDisconnected(string userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var set)) return false;
        lock (set)
        {
            set.Remove(connectionId);
            if (set.Count == 0)
            {
                _connections.TryRemove(userId, out _);
                return true;
            }
            return false;
        }
    }

    public bool IsOnline(string userId) =>
        _connections.TryGetValue(userId, out var set) && set.Count > 0;

    public IReadOnlyCollection<string> GetOnlineUsers() =>
        _connections.Where(kvp => kvp.Value.Count > 0).Select(kvp => kvp.Key).ToList();
}

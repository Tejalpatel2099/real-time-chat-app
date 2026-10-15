using ChatApp.Api.Services;

namespace ChatApp.Tests;

public class PresenceTrackerTests
{
    private readonly IPresenceTracker _tracker = new PresenceTracker();

    [Fact]
    public void FirstConnection_MarksUserOnline()
    {
        Assert.True(_tracker.UserConnected("user-1", "conn-a"));
        Assert.True(_tracker.IsOnline("user-1"));
    }

    [Fact]
    public void SecondConnection_DoesNotRebroadcast()
    {
        _tracker.UserConnected("user-1", "conn-a");

        // Already online -> returns false, so the hub won't broadcast UserOnline twice.
        Assert.False(_tracker.UserConnected("user-1", "conn-b"));
        Assert.True(_tracker.IsOnline("user-1"));
    }

    [Fact]
    public void DisconnectingOneOfMany_KeepsUserOnline()
    {
        _tracker.UserConnected("user-1", "conn-a");
        _tracker.UserConnected("user-1", "conn-b");

        Assert.False(_tracker.UserDisconnected("user-1", "conn-a"));
        Assert.True(_tracker.IsOnline("user-1"));
    }

    [Fact]
    public void LastDisconnect_MarksUserOffline()
    {
        _tracker.UserConnected("user-1", "conn-a");

        Assert.True(_tracker.UserDisconnected("user-1", "conn-a"));
        Assert.False(_tracker.IsOnline("user-1"));
    }

    [Fact]
    public void GetOnlineUsers_ListsOnlyConnectedUsers()
    {
        _tracker.UserConnected("user-1", "conn-a");
        _tracker.UserConnected("user-2", "conn-b");
        _tracker.UserDisconnected("user-2", "conn-b");

        var online = _tracker.GetOnlineUsers();
        Assert.Contains("user-1", online);
        Assert.DoesNotContain("user-2", online);
    }

    [Fact]
    public void DisconnectingUnknownConnection_IsSafe()
    {
        Assert.False(_tracker.UserDisconnected("nobody", "conn-x"));
        Assert.False(_tracker.IsOnline("nobody"));
    }
}

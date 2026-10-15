using System.Security.Claims;
using ChatApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChatApp.Api.Hubs;

/// <summary>
/// Real-time messaging hub. Clients join SignalR groups named after the
/// conversation id, so messages fan out only to members of that conversation.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly IConversationService _conversations;
    private readonly IMessageService _messages;
    private readonly IPresenceTracker _presence;
    private readonly IAuthService _auth;

    public ChatHub(
        IConversationService conversations,
        IMessageService messages,
        IPresenceTracker presence,
        IAuthService auth)
    {
        _conversations = conversations;
        _messages = messages;
        _presence = presence;
        _auth = auth;
    }

    private string UserId => Context.UserIdentifier
        ?? throw new HubException("Missing user identity.");

    private Guid UserGuid => Guid.Parse(UserId);

    public override async Task OnConnectedAsync()
    {
        if (_presence.UserConnected(UserId, Context.ConnectionId))
        {
            var user = await _auth.GetByIdAsync(UserGuid);
            await Clients.All.SendAsync("UserOnline", new { userId = UserId, username = user?.Username });
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_presence.UserDisconnected(UserId, Context.ConnectionId))
        {
            var user = await _auth.GetByIdAsync(UserGuid);
            await Clients.All.SendAsync("UserOffline", new { userId = UserId, username = user?.Username });
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Subscribe this connection to a conversation's message stream.</summary>
    public async Task JoinConversation(string conversationId)
    {
        var id = ParseId(conversationId);
        if (!await _conversations.IsMemberAsync(id, UserGuid))
            throw new HubException("You are not a member of this conversation.");
        await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);
    }

    public async Task LeaveConversation(string conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId);
    }

    /// <summary>Persist a message and broadcast it to the conversation group.</summary>
    public async Task SendMessage(string conversationId, string content)
    {
        var id = ParseId(conversationId);
        if (!await _conversations.IsMemberAsync(id, UserGuid))
            throw new HubException("You are not a member of this conversation.");
        var message = await _messages.SendAsync(id, UserGuid, content);
        await Clients.Group(conversationId).SendAsync("ReceiveMessage", message);
    }

    public async Task TypingStart(string conversationId)
    {
        var id = ParseId(conversationId);
        if (!await _conversations.IsMemberAsync(id, UserGuid)) return;
        var username = await UsernameAsync();
        await Clients.OthersInGroup(conversationId)
            .SendAsync("UserTyping", new { userId = UserId, username, conversationId, isTyping = true });
    }

    public async Task TypingStop(string conversationId)
    {
        var id = ParseId(conversationId);
        if (!await _conversations.IsMemberAsync(id, UserGuid)) return;
        var username = await UsernameAsync();
        await Clients.OthersInGroup(conversationId)
            .SendAsync("UserTyping", new { userId = UserId, username, conversationId, isTyping = false });
    }

    private async Task<string?> UsernameAsync()
    {
        var user = await _auth.GetByIdAsync(UserGuid);
        return user?.Username;
    }

    private static Guid ParseId(string conversationId)
    {
        if (!Guid.TryParse(conversationId, out var id))
            throw new HubException("Invalid conversation id.");
        return id;
    }
}

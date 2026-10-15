using ChatApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/presence")]
public class PresenceController : ControllerBase
{
    private readonly IPresenceTracker _presence;

    public PresenceController(IPresenceTracker presence) => _presence = presence;

    /// <summary>Ids of users currently online (any active SignalR connection).</summary>
    [HttpGet("online")]
    public ActionResult<IReadOnlyCollection<string>> Online() => Ok(_presence.GetOnlineUsers());
}

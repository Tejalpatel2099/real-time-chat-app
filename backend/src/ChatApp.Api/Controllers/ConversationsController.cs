using System.Security.Claims;
using ChatApp.Api.Dtos;
using ChatApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/conversations")]
public class ConversationsController : ControllerBase
{
    private readonly IConversationService _conversations;

    public ConversationsController(IConversationService conversations) => _conversations = conversations;

    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<ActionResult<List<ConversationDto>>> GetMine() =>
        Ok(await _conversations.GetMineAsync(UserId));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ConversationDto>> GetById(Guid id)
    {
        var conversation = await _conversations.GetByIdAsync(id, UserId);
        return conversation is null ? NotFound() : Ok(conversation);
    }

    [HttpPost]
    public async Task<ActionResult<ConversationDto>> Create([FromBody] CreateConversationRequest request)
    {
        var (ok, error, conversation) = await _conversations.CreateAsync(UserId, request);
        if (!ok) return BadRequest(new { error });
        return CreatedAtAction(nameof(GetById), new { id = conversation!.Id }, conversation);
    }

    [HttpPost("{id:guid}/members")]
    public async Task<ActionResult> AddMembers(Guid id, [FromBody] AddMembersRequest request)
    {
        var (ok, error) = await _conversations.AddMembersAsync(id, UserId, request.Usernames);
        return ok ? Ok(new { added = true }) : BadRequest(new { error });
    }
}

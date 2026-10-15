using System.Security.Claims;
using ChatApp.Api.Dtos;
using ChatApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/messages")]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messages;
    private readonly IConversationService _conversations;

    public MessagesController(IMessageService messages, IConversationService conversations)
    {
        _messages = messages;
        _conversations = conversations;
    }

    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private async Task<bool> CanReadAsync(Guid conversationId) =>
        await _conversations.IsMemberAsync(conversationId, UserId);

    [HttpPost]
    public async Task<ActionResult<MessageDto>> Send([FromBody] SendMessageRequest request)
    {
        if (!await CanReadAsync(request.ConversationId)) return Forbid();
        try
        {
            var message = await _messages.SendAsync(request.ConversationId, UserId, request.Content);
            return StatusCode(StatusCodes.Status201Created, message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<MessagePageDto>> History(
        [FromQuery] Guid conversationId,
        [FromQuery] DateTime? before,
        [FromQuery] int limit = 50)
    {
        if (!await CanReadAsync(conversationId)) return Forbid();
        return Ok(await _messages.GetHistoryAsync(conversationId, before, limit));
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<MessageDto>>> Search(
        [FromQuery] Guid conversationId,
        [FromQuery] string q = "")
    {
        if (!await CanReadAsync(conversationId)) return Forbid();
        return Ok(await _messages.SearchAsync(conversationId, q));
    }
}

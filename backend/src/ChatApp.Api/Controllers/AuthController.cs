using ChatApp.Api.Dtos;
using ChatApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    public async Task<ActionResult<UserDto>> Register([FromBody] RegisterRequest request)
    {
        var (ok, error, user) = await _auth.RegisterAsync(request);
        if (!ok) return BadRequest(new { error });
        return CreatedAtAction(nameof(Me), user);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var (ok, error, token, user) = await _auth.LoginAsync(request);
        if (!ok) return Unauthorized(new { error });
        return Ok(new AuthResponse(token!, user!));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var user = await _auth.GetByIdAsync(userId);
        return user is null ? Unauthorized() : Ok(user);
    }
}

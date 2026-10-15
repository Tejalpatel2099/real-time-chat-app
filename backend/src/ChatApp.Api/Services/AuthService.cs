using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ChatApp.Api.Data;
using ChatApp.Api.Dtos;
using ChatApp.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ChatApp.Api.Services;

public interface IAuthService
{
    Task<(bool Ok, string? Error, UserDto? User)> RegisterAsync(RegisterRequest request);
    Task<(bool Ok, string? Error, string? Token, UserDto? User)> LoginAsync(LoginRequest request);
    Task<UserDto?> GetByIdAsync(Guid userId);
}

public class AuthService : IAuthService
{
    private readonly ChatDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(ChatDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<(bool Ok, string? Error, UserDto? User)> RegisterAsync(RegisterRequest request)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim();

        if (await _db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower()))
            return (false, "Username is already taken.", null);
        if (await _db.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower()))
            return (false, "Email is already registered.", null);

        var user = new User
        {
            Username = username,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return (true, null, ToDto(user));
    }

    public async Task<(bool Ok, string? Error, string? Token, UserDto? User)> LoginAsync(LoginRequest request)
    {
        var id = request.UsernameOrEmail.Trim().ToLower();
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Username.ToLower() == id || u.Email.ToLower() == id);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return (false, "Invalid username/email or password.", null, null);

        return (true, null, CreateToken(user), ToDto(user));
    }

    public async Task<UserDto?> GetByIdAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        return user is null ? null : ToDto(user);
    }

    private string CreateToken(User user)
    {
        var jwt = _config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(double.Parse(jwt["ExpiryMinutes"] ?? "120")),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static UserDto ToDto(User u) => new(u.Id, u.Username, u.Email, u.DisplayName);
}

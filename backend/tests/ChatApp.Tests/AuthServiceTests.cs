using ChatApp.Api.Dtos;

namespace ChatApp.Tests;

public class AuthServiceTests : TestBase
{
    [Fact]
    public async Task Register_CreatesUser_WithHashedPassword()
    {
        var (ok, error, user) = await Auth.RegisterAsync(new(
            "tejal", "tejal@example.com", "secret123", "Tejal Patel"));

        Assert.True(ok, error);
        Assert.NotNull(user);
        Assert.Equal("tejal", user.Username);

        var stored = Db.Users.Single(u => u.Username == "tejal");
        Assert.NotEqual("secret123", stored.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("secret123", stored.PasswordHash));
    }

    [Fact]
    public async Task Register_DuplicateUsername_Fails()
    {
        await Auth.RegisterAsync(new("tejal", "a@example.com", "secret123", "Tejal"));

        var (ok, error, _) = await Auth.RegisterAsync(new("Tejal", "b@example.com", "secret123", "Other"));

        Assert.False(ok);
        Assert.Contains("Username", error);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Fails()
    {
        await Auth.RegisterAsync(new("tejal", "tejal@example.com", "secret123", "Tejal"));

        var (ok, error, _) = await Auth.RegisterAsync(new("other", "TEJAL@example.com", "secret123", "Other"));

        Assert.False(ok);
        Assert.Contains("Email", error);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenAndUser()
    {
        await Auth.RegisterAsync(new("tejal", "tejal@example.com", "secret123", "Tejal Patel"));

        var (ok, error, token, user) = await Auth.LoginAsync(new("tejal", "secret123"));

        Assert.True(ok, error);
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal("tejal", user!.Username);
        // A JWT has three dot-separated segments.
        Assert.Equal(3, token!.Split('.').Length);
    }

    [Fact]
    public async Task Login_ByEmail_Works()
    {
        await Auth.RegisterAsync(new("tejal", "tejal@example.com", "secret123", "Tejal Patel"));

        var (ok, _, _, user) = await Auth.LoginAsync(new("tejal@example.com", "secret123"));

        Assert.True(ok);
        Assert.Equal("tejal", user!.Username);
    }

    [Fact]
    public async Task Login_WrongPassword_Fails()
    {
        await Auth.RegisterAsync(new("tejal", "tejal@example.com", "secret123", "Tejal"));

        var (ok, error, token, _) = await Auth.LoginAsync(new("tejal", "wrong-password"));

        Assert.False(ok);
        Assert.Null(token);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task Login_UnknownUser_Fails()
    {
        var (ok, _, _, _) = await Auth.LoginAsync(new("ghost", "whatever"));
        Assert.False(ok);
    }
}

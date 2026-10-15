using ChatApp.Api.Data;
using ChatApp.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ChatApp.Tests;

/// <summary>Shared helpers: fresh InMemory database + test JWT config per test.</summary>
public abstract class TestBase : IDisposable
{
    protected readonly ChatDbContext Db;
    protected readonly IAuthService Auth;
    protected readonly IConversationService Conversations;
    protected readonly IMessageService Messages;

    protected TestBase()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        Db = new ChatDbContext(options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-key-not-for-production-0123456789abcdef0123456789abcdef",
                ["Jwt:Issuer"] = "ChatApp.Tests",
                ["Jwt:Audience"] = "ChatApp.Tests",
                ["Jwt:ExpiryMinutes"] = "60",
            })
            .Build();

        Auth = new AuthService(Db, config);
        Conversations = new ConversationService(Db);
        Messages = new MessageService(Db);
    }

    protected async Task<Guid> RegisterUserAsync(string username)
    {
        var (ok, error, user) = await Auth.RegisterAsync(new(
            username, $"{username}@example.com", "password123", username));
        if (!ok) throw new InvalidOperationException($"Setup failed: {error}");
        return user!.Id;
    }

    public void Dispose() => Db.Dispose();
}

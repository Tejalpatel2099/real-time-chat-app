using ChatApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Api.Data;

/// <summary>
/// Seeds a small demo dataset when the SeedDemoData environment variable is "true".
/// Logins: demo1 / demo2, password "demo1234" for both.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(ChatDbContext db)
    {
        if (await db.Users.AnyAsync()) return;

        var demo1 = new User
        {
            Username = "demo1",
            Email = "demo1@example.com",
            DisplayName = "Demo One",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("demo1234"),
        };
        var demo2 = new User
        {
            Username = "demo2",
            Email = "demo2@example.com",
            DisplayName = "Demo Two",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("demo1234"),
        };
        db.Users.AddRange(demo1, demo2);

        var general = new Conversation
        {
            Name = "General",
            IsGroup = true,
            CreatedById = demo1.Id,
        };
        db.Conversations.Add(general);
        db.ConversationMembers.AddRange(
            new ConversationMember { ConversationId = general.Id, UserId = demo1.Id },
            new ConversationMember { ConversationId = general.Id, UserId = demo2.Id });

        var now = DateTime.UtcNow;
        var texts = new[]
        {
            (demo1, "Hey! Welcome to the General channel 👋"),
            (demo2, "Thanks! This chat runs on SignalR — messages arrive in real time."),
            (demo1, "Yep, and history is paginated from SQL Server. Scroll up to load older messages."),
            (demo2, "Try the search box too — it uses a LIKE query over the message content."),
            (demo1, "Green dots mean someone is online. That's the presence tracker at work."),
            (demo2, "Nice. Start typing and watch the typing indicator…"),
        };
        for (var i = 0; i < texts.Length; i++)
        {
            db.Messages.Add(new Message
            {
                ConversationId = general.Id,
                SenderId = texts[i].Item1.Id,
                Content = texts[i].Item2,
                SentAt = now.AddMinutes(-(texts.Length - i) * 3),
            });
        }

        await db.SaveChangesAsync();
    }
}

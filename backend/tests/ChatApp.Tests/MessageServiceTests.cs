using ChatApp.Api.Dtos;
using ChatApp.Api.Models;

namespace ChatApp.Tests;

public class MessageServiceTests : TestBase
{
    private async Task<(Guid ConversationId, Guid AliceId, Guid BobId)> SeedConversationAsync()
    {
        var alice = await RegisterUserAsync("alice");
        var bob = await RegisterUserAsync("bob");
        var (ok, error, conversation) = await Conversations.CreateAsync(
            alice, new CreateConversationRequest("Team", true, new List<string> { "bob" }));
        if (!ok) throw new InvalidOperationException($"Setup failed: {error}");
        return (conversation!.Id, alice, bob);
    }

    [Fact]
    public async Task Send_PersistsMessage_WithSenderInfo()
    {
        var (conversationId, aliceId, _) = await SeedConversationAsync();

        var dto = await Messages.SendAsync(conversationId, aliceId, "Hello, world!");

        Assert.True(dto.Id > 0);
        Assert.Equal("Hello, world!", dto.Content);
        Assert.Equal("alice", dto.SenderUsername);
        Assert.Equal(conversationId, dto.ConversationId);
        Assert.Equal(1, Db.Messages.Count());
    }

    [Fact]
    public async Task Send_EmptyContent_Throws()
    {
        var (conversationId, aliceId, _) = await SeedConversationAsync();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Messages.SendAsync(conversationId, aliceId, "   "));
    }

    [Fact]
    public async Task GetHistory_Paginates_WithCursor_AndHasMore()
    {
        var (conversationId, aliceId, bobId) = await SeedConversationAsync();
        var baseTime = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 120; i++)
        {
            Db.Messages.Add(new Message
            {
                ConversationId = conversationId,
                SenderId = i % 2 == 0 ? aliceId : bobId,
                Content = $"message {i:D3}",
                SentAt = baseTime.AddSeconds(i),
            });
        }
        await Db.SaveChangesAsync();

        // Page 1: newest 50.
        var page1 = await Messages.GetHistoryAsync(conversationId, null, 50);
        Assert.Equal(50, page1.Messages.Count);
        Assert.True(page1.HasMore);
        Assert.Equal("message 070", page1.Messages.First().Content);
        Assert.Equal("message 119", page1.Messages.Last().Content);
        // Chronological (ascending) within the page.
        Assert.True(page1.Messages.Zip(page1.Messages.Skip(1), (a, b) => a.SentAt <= b.SentAt).All(x => x));

        // Page 2: next older 50, using the oldest message of page 1 as cursor.
        var page2 = await Messages.GetHistoryAsync(conversationId, page1.Messages.First().SentAt, 50);
        Assert.Equal(50, page2.Messages.Count);
        Assert.True(page2.HasMore);
        Assert.Equal("message 020", page2.Messages.First().Content);
        Assert.Equal("message 069", page2.Messages.Last().Content);

        // Page 3: final 20, no more pages.
        var page3 = await Messages.GetHistoryAsync(conversationId, page2.Messages.First().SentAt, 50);
        Assert.Equal(20, page3.Messages.Count);
        Assert.False(page3.HasMore);
        Assert.Equal("message 000", page3.Messages.First().Content);

        // No overlap and full coverage across pages.
        var allIds = page1.Messages.Concat(page2.Messages).Concat(page3.Messages).Select(m => m.Id).ToList();
        Assert.Equal(120, allIds.Distinct().Count());
    }

    [Fact]
    public async Task GetHistory_Limit_IsClamped()
    {
        var (conversationId, aliceId, _) = await SeedConversationAsync();
        await Messages.SendAsync(conversationId, aliceId, "one");

        var page = await Messages.GetHistoryAsync(conversationId, null, 500);
        Assert.Single(page.Messages);
        Assert.False(page.HasMore);
    }

    [Fact]
    public async Task Search_ReturnsMatchingMessages_OnlyInConversation()
    {
        var (conversationId, aliceId, bobId) = await SeedConversationAsync();
        await Messages.SendAsync(conversationId, aliceId, "I love pineapple pizza");
        await Messages.SendAsync(conversationId, bobId, "Pineapple belongs on pizza, fight me");
        await Messages.SendAsync(conversationId, aliceId, "Let's talk about databases");

        var other = await Conversations.CreateAsync(
            aliceId, new CreateConversationRequest("Other", true, new List<string> { "bob" }));
        await Messages.SendAsync(other.Conversation!.Id, bobId, "pineapple in another room");

        var results = await Messages.SearchAsync(conversationId, "pineapple");

        Assert.Equal(2, results.Count);
        Assert.All(results, m => Assert.Contains("pineapple", m.Content, StringComparison.OrdinalIgnoreCase));
        Assert.All(results, m => Assert.Equal(conversationId, m.ConversationId));
    }

    [Fact]
    public async Task Search_EmptyQuery_ReturnsNothing()
    {
        var (conversationId, _, _) = await SeedConversationAsync();
        Assert.Empty(await Messages.SearchAsync(conversationId, "   "));
    }
}

using ChatApp.Api.Data;
using ChatApp.Api.Dtos;
using ChatApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Api.Services;

public interface IMessageService
{
    Task<MessageDto> SendAsync(Guid conversationId, Guid senderId, string content);
    Task<MessagePageDto> GetHistoryAsync(Guid conversationId, DateTime? before, int limit);
    Task<List<MessageDto>> SearchAsync(Guid conversationId, string query);
}

public class MessageService : IMessageService
{
    private const int MaxContentLength = 2000;
    private const int DefaultLimit = 50;
    private const int MaxLimit = 100;

    private readonly ChatDbContext _db;

    public MessageService(ChatDbContext db) => _db = db;

    public async Task<MessageDto> SendAsync(Guid conversationId, Guid senderId, string content)
    {
        var text = (content ?? string.Empty).Trim();
        if (text.Length == 0) throw new ArgumentException("Message content cannot be empty.", nameof(content));
        if (text.Length > MaxContentLength) throw new ArgumentException($"Message content cannot exceed {MaxContentLength} characters.", nameof(content));

        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = text,
            SentAt = DateTime.UtcNow,
        };
        _db.Messages.Add(message);
        await _db.SaveChangesAsync();

        var sender = await _db.Users.FindAsync(senderId);
        return ToDto(message, sender!);
    }

    /// <summary>
    /// Cursor-paginated history. <paramref name="before"/> is an exclusive upper bound on
    /// SentAt (ISO-8601 from the query string); null returns the most recent page.
    /// Rows are fetched newest-first (which the (ConversationId, SentAt) index serves),
    /// then reversed so the client receives them in chronological order.
    /// </summary>
    public async Task<MessagePageDto> GetHistoryAsync(Guid conversationId, DateTime? before, int limit)
    {
        limit = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);

        var query = _db.Messages
            .Where(m => m.ConversationId == conversationId);
        if (before.HasValue)
            query = query.Where(m => m.SentAt < before.Value);

        var rows = await query
            .Include(m => m.Sender)
            .OrderByDescending(m => m.SentAt)
            .ThenByDescending(m => m.Id)
            .Take(limit + 1)
            .ToListAsync();

        var hasMore = rows.Count > limit;
        var page = rows.Take(limit).Reverse().Select(m => ToDto(m, m.Sender!)).ToList();
        return new MessagePageDto(page, hasMore);
    }

    public async Task<List<MessageDto>> SearchAsync(Guid conversationId, string query)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length == 0) return new List<MessageDto>();

        var pattern = $"%{q.Replace("%", "[%]").Replace("_", "[_]")}%";
        var rows = await _db.Messages
            .Where(m => m.ConversationId == conversationId && EF.Functions.Like(m.Content, pattern))
            .Include(m => m.Sender)
            .OrderByDescending(m => m.SentAt)
            .ThenByDescending(m => m.Id)
            .Take(50)
            .ToListAsync();

        return rows.Select(m => ToDto(m, m.Sender!)).ToList();
    }

    private static MessageDto ToDto(Message m, User sender) => new(
        m.Id, m.ConversationId, m.SenderId,
        sender.Username, sender.DisplayName,
        m.Content, m.SentAt);
}

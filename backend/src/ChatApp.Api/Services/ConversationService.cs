using ChatApp.Api.Data;
using ChatApp.Api.Dtos;
using ChatApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Api.Services;

public interface IConversationService
{
    Task<List<ConversationDto>> GetMineAsync(Guid userId);
    Task<ConversationDto?> GetByIdAsync(Guid conversationId, Guid userId);
    Task<(bool Ok, string? Error, ConversationDto? Conversation)> CreateAsync(Guid creatorId, CreateConversationRequest request);
    Task<(bool Ok, string? Error)> AddMembersAsync(Guid conversationId, Guid requesterId, List<string> usernames);
    Task<bool> IsMemberAsync(Guid conversationId, Guid userId);
}

public class ConversationService : IConversationService
{
    private readonly ChatDbContext _db;

    public ConversationService(ChatDbContext db) => _db = db;

    public async Task<List<ConversationDto>> GetMineAsync(Guid userId)
    {
        var conversations = await _db.Conversations
            .Where(c => c.Members.Any(m => m.UserId == userId))
            .Include(c => c.Members).ThenInclude(m => m.User)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var dtos = new List<ConversationDto>();
        foreach (var c in conversations)
            dtos.Add(await ToDtoAsync(c, userId));
        return dtos;
    }

    public async Task<ConversationDto?> GetByIdAsync(Guid conversationId, Guid userId)
    {
        var c = await _db.Conversations
            .Include(x => x.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(x => x.Id == conversationId);
        if (c is null || !c.Members.Any(m => m.UserId == userId)) return null;
        return await ToDtoAsync(c, userId);
    }

    public async Task<(bool Ok, string? Error, ConversationDto? Conversation)> CreateAsync(
        Guid creatorId, CreateConversationRequest request)
    {
        var wanted = (request.MemberUsernames ?? new List<string>())
            .Select(u => u.Trim())
            .Where(u => u.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Match case-insensitively in memory so behavior is identical on
        // SQL Server (case-insensitive collation) and the InMemory test provider.
        var allUsers = await _db.Users.ToListAsync();
        var users = allUsers
            .Where(u => wanted.Contains(u.Username, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var missing = wanted
            .Where(w => !users.Any(u => u.Username.Equals(w, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (missing.Count > 0)
            return (false, $"Unknown user(s): {string.Join(", ", missing)}", null);

        var conversation = new Conversation
        {
            Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim(),
            IsGroup = request.IsGroup,
            CreatedById = creatorId,
        };
        _db.Conversations.Add(conversation);

        var memberIds = users.Select(u => u.Id).Append(creatorId).Distinct().ToList();
        foreach (var id in memberIds)
            _db.ConversationMembers.Add(new ConversationMember { ConversationId = conversation.Id, UserId = id });

        await _db.SaveChangesAsync();

        var created = await _db.Conversations
            .Include(c => c.Members).ThenInclude(m => m.User)
            .FirstAsync(c => c.Id == conversation.Id);
        return (true, null, await ToDtoAsync(created, creatorId));
    }

    public async Task<(bool Ok, string? Error)> AddMembersAsync(
        Guid conversationId, Guid requesterId, List<string> usernames)
    {
        var conversation = await _db.Conversations
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == conversationId);
        if (conversation is null) return (false, "Conversation not found.");
        if (!conversation.Members.Any(m => m.UserId == requesterId))
            return (false, "Only members can add people to this conversation.");

        var wanted = (usernames ?? new List<string>())
            .Select(u => u.Trim()).Where(u => u.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var allUsers = await _db.Users.ToListAsync();
        var users = allUsers
            .Where(u => wanted.Contains(u.Username, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var missing = wanted
            .Where(w => !users.Any(u => u.Username.Equals(w, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (missing.Count > 0) return (false, $"Unknown user(s): {string.Join(", ", missing)}");

        var existing = conversation.Members.Select(m => m.UserId).ToHashSet();
        foreach (var u in users.Where(u => !existing.Contains(u.Id)))
            _db.ConversationMembers.Add(new ConversationMember { ConversationId = conversationId, UserId = u.Id });

        await _db.SaveChangesAsync();
        return (true, null);
    }

    public Task<bool> IsMemberAsync(Guid conversationId, Guid userId) =>
        _db.ConversationMembers.AnyAsync(m => m.ConversationId == conversationId && m.UserId == userId);

    private async Task<ConversationDto> ToDtoAsync(Conversation c, Guid viewerId)
    {
        var members = c.Members
            .Select(m => new MemberDto(m.UserId, m.User!.Username, m.User.DisplayName))
            .OrderBy(m => m.DisplayName)
            .ToList();

        var last = await _db.Messages
            .Where(m => m.ConversationId == c.Id)
            .Include(m => m.Sender)
            .OrderByDescending(m => m.SentAt)
            .ThenByDescending(m => m.Id)
            .FirstOrDefaultAsync();

        // Direct (1:1) threads show the other person's name when no explicit name is set.
        string? name = c.Name;
        if (name is null && !c.IsGroup)
            name = members.FirstOrDefault(m => m.UserId != viewerId)?.DisplayName ?? "Direct message";

        return new ConversationDto(
            c.Id, name, c.IsGroup, members.Count, members,
            last is null ? null : new LastMessageDto(last.Content, last.SentAt, last.Sender!.DisplayName));
    }
}

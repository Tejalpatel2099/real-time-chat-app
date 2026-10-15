namespace ChatApp.Api.Models;

/// <summary>Application user. Passwords are stored as BCrypt hashes, never plaintext.</summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ConversationMember> Memberships { get; set; } = new List<ConversationMember>();
    public ICollection<Message> SentMessages { get; set; } = new List<Message>();
}

/// <summary>A chat conversation: either a group chat (IsGroup) or a 1:1 direct message thread.</summary>
public class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Name { get; set; }
    public bool IsGroup { get; set; }
    public Guid CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? CreatedBy { get; set; }
    public ICollection<ConversationMember> Members { get; set; } = new List<ConversationMember>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}

/// <summary>Join entity between users and conversations (composite key).</summary>
public class ConversationMember
{
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Conversation? Conversation { get; set; }
    public User? User { get; set; }
}

public class Message
{
    public long Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public Conversation? Conversation { get; set; }
    public User? Sender { get; set; }
}

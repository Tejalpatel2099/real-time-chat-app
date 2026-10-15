using System.ComponentModel.DataAnnotations;

namespace ChatApp.Api.Dtos;

// ---- Auth ----
public record RegisterRequest(
    [MinLength(3)] string Username,
    [EmailAddress] string Email,
    [MinLength(6)] string Password,
    [Required, MinLength(1)] string DisplayName);

public record LoginRequest(
    [Required] string UsernameOrEmail,
    [Required] string Password);

public record UserDto(Guid Id, string Username, string Email, string DisplayName);

public record AuthResponse(string Token, UserDto User);

// ---- Conversations ----
public record CreateConversationRequest(
    string? Name,
    bool IsGroup,
    List<string> MemberUsernames);

public record AddMembersRequest(List<string> Usernames);

public record MemberDto(Guid UserId, string Username, string DisplayName);

public record LastMessageDto(string Content, DateTime SentAt, string SenderDisplayName);

public record ConversationDto(
    Guid Id,
    string? Name,
    bool IsGroup,
    int MemberCount,
    List<MemberDto> Members,
    LastMessageDto? LastMessage);

// ---- Messages ----
public record SendMessageRequest(Guid ConversationId, [Required, MinLength(1)] string Content);

public record MessageDto(
    long Id,
    Guid ConversationId,
    Guid SenderId,
    string SenderUsername,
    string SenderDisplayName,
    string Content,
    DateTime SentAt);

public record MessagePageDto(List<MessageDto> Messages, bool HasMore);

export interface UserDto {
  id: string;
  username: string;
  email: string;
  displayName: string;
}

export interface MemberDto {
  userId: string;
  username: string;
  displayName: string;
}

export interface LastMessageDto {
  content: string;
  sentAt: string;
  senderDisplayName: string;
}

export interface ConversationDto {
  id: string;
  name: string | null;
  isGroup: boolean;
  memberCount: number;
  members: MemberDto[];
  lastMessage: LastMessageDto | null;
}

export interface MessageDto {
  id: number;
  conversationId: string;
  senderId: string;
  senderUsername: string;
  senderDisplayName: string;
  content: string;
  sentAt: string;
}

export interface MessagePageDto {
  messages: MessageDto[];
  hasMore: boolean;
}

/** Client-side message with local send-state for optimistic rendering. */
export interface ChatMessage extends MessageDto {
  status?: 'sending' | 'sent' | 'failed';
  tempId?: string;
}

export interface TypingEvent {
  userId: string;
  username: string;
  conversationId: string;
  isTyping: boolean;
}

export interface PresenceEvent {
  userId: string;
  username: string;
}

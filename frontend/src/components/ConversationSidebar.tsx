import type { ConversationDto } from '../lib/types';

interface Props {
  conversations: ConversationDto[];
  selectedId: string | null;
  onSelect: (id: string) => void;
  onlineUsers: Set<string>;
  currentUserId: string;
  displayName: string;
  onNewGroup: () => void;
  onLogout: () => void;
}

function timeAgo(iso: string): string {
  const mins = Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 60000));
  if (mins < 1) return 'now';
  if (mins < 60) return `${mins}m`;
  const hours = Math.floor(mins / 60);
  if (hours < 24) return `${hours}h`;
  const days = Math.floor(hours / 24);
  if (days < 7) return `${days}d`;
  return new Date(iso).toLocaleDateString();
}

export default function ConversationSidebar({
  conversations,
  selectedId,
  onSelect,
  onlineUsers,
  currentUserId,
  displayName,
  onNewGroup,
  onLogout,
}: Props) {
  return (
    <aside className="sidebar">
      <div className="sidebar-header">
        <div className="brand">ChatApp</div>
        <button className="btn btn-small" onClick={onNewGroup}>
          + New
        </button>
      </div>
      <div className="convo-list">
        {conversations.map((c) => {
          const someoneOnline = c.members.some(
            (m) => m.userId !== currentUserId && onlineUsers.has(m.userId),
          );
          return (
            <button
              key={c.id}
              className={`convo-item${c.id === selectedId ? ' selected' : ''}`}
              onClick={() => onSelect(c.id)}
            >
              <span className="avatar">
                {(c.name ?? '?').slice(0, 1).toUpperCase()}
                {someoneOnline && <span className="online-dot" />}
              </span>
              <span className="convo-meta">
                <span className="convo-name">{c.name ?? 'Chat'}</span>
                <span className="convo-preview">
                  {c.lastMessage
                    ? `${c.lastMessage.senderDisplayName}: ${c.lastMessage.content}`
                    : 'No messages yet'}
                </span>
              </span>
              {c.lastMessage && (
                <span className="convo-time">{timeAgo(c.lastMessage.sentAt)}</span>
              )}
            </button>
          );
        })}
        {conversations.length === 0 && (
          <div className="empty-hint">No conversations yet — start a new one!</div>
        )}
      </div>
      <div className="sidebar-footer">
        <span className="me">{displayName}</span>
        <button className="btn btn-ghost" onClick={onLogout}>
          Log out
        </button>
      </div>
    </aside>
  );
}

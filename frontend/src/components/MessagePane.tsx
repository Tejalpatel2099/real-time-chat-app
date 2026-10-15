import { useEffect, useRef, useState } from 'react';
import { api } from '../lib/api';
import type { ChatMessage, ConversationDto, MessageDto } from '../lib/types';

interface Props {
  conversation: ConversationDto;
  messages: ChatMessage[];
  hasMore: boolean;
  loadingOlder: boolean;
  onLoadOlder: () => void;
  onSend: (text: string) => void;
  onRetry: (tempId: string) => void;
  onTyping: () => void;
  onTypingStopped: () => void;
  typingNames: string[];
  onlineUsers: Set<string>;
  currentUserId: string;
}

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

export default function MessagePane({
  conversation,
  messages,
  hasMore,
  loadingOlder,
  onLoadOlder,
  onSend,
  onRetry,
  onTyping,
  onTypingStopped,
  typingNames,
  onlineUsers,
  currentUserId,
}: Props) {
  const [draft, setDraft] = useState('');
  const [searchOpen, setSearchOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<MessageDto[]>([]);
  const [searching, setSearching] = useState(false);
  const listRef = useRef<HTMLDivElement>(null);
  const anchorRef = useRef<{ height: number; top: number } | null>(null);

  const handleScroll = () => {
    const el = listRef.current;
    if (!el) return;
    if (el.scrollTop < 60 && hasMore && !loadingOlder && !searchOpen) {
      anchorRef.current = { height: el.scrollHeight, top: el.scrollTop };
      onLoadOlder();
    }
  };

  // Keep the user's scroll position when older messages are prepended,
  // otherwise stick to the newest message.
  useEffect(() => {
    const el = listRef.current;
    if (!el) return;
    const anchor = anchorRef.current;
    if (anchor) {
      el.scrollTop = el.scrollHeight - anchor.height + anchor.top;
      anchorRef.current = null;
    } else {
      el.scrollTop = el.scrollHeight;
    }
  }, [messages, conversation.id]);

  // Debounced in-conversation search.
  useEffect(() => {
    if (!searchOpen || query.trim().length < 2) {
      setResults([]);
      setSearching(false);
      return;
    }
    setSearching(true);
    const timer = window.setTimeout(async () => {
      try {
        const res = await api.get<MessageDto[]>('/messages/search', {
          params: { conversationId: conversation.id, q: query.trim() },
        });
        setResults(res.data);
      } catch {
        setResults([]);
      } finally {
        setSearching(false);
      }
    }, 300);
    return () => window.clearTimeout(timer);
  }, [query, searchOpen, conversation.id]);

  const send = () => {
    const text = draft.trim();
    if (!text) return;
    onSend(text);
    setDraft('');
    onTypingStopped();
  };

  const shownTyping = typingNames.filter(Boolean);

  return (
    <main className="chat-main">
      <header className="chat-header">
        <div>
          <div className="chat-title">{conversation.name ?? 'Chat'}</div>
          <div className="chat-members">
            {conversation.members.map((m) => (
              <span key={m.userId} className="member-chip">
                <span
                  className={`presence-dot${onlineUsers.has(m.userId) ? ' on' : ''}`}
                />
                {m.displayName}
              </span>
            ))}
          </div>
        </div>
        <button
          className="btn btn-ghost"
          onClick={() => {
            setSearchOpen((v) => !v);
            setQuery('');
            setResults([]);
          }}
        >
          {searchOpen ? 'Close search' : 'Search'}
        </button>
      </header>

      {searchOpen && (
        <div className="search-bar">
          <input
            className="input"
            placeholder="Search messages in this conversation…"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            autoFocus
          />
        </div>
      )}

      <div className="messages" ref={listRef} onScroll={handleScroll}>
        {searchOpen ? (
          <div className="search-results">
            {searching && <div className="muted">Searching…</div>}
            {!searching && query.trim().length >= 2 && results.length === 0 && (
              <div className="muted">No matches.</div>
            )}
            {!searching && query.trim().length < 2 && (
              <div className="muted">Type at least 2 characters to search.</div>
            )}
            {results.map((m) => (
              <div key={m.id} className="search-hit">
                <span className="search-hit-sender">{m.senderDisplayName}</span>
                <span className="search-hit-text">{m.content}</span>
                <span className="search-hit-time">{formatTime(m.sentAt)}</span>
              </div>
            ))}
          </div>
        ) : (
          <>
            {loadingOlder && <div className="muted center">Loading older messages…</div>}
            {messages.map((m) => {
              const own = m.senderId === currentUserId;
              return (
                <div key={m.tempId ?? m.id} className={`msg${own ? ' own' : ''}`}>
                  {!own && conversation.isGroup && (
                    <div className="msg-sender">{m.senderDisplayName}</div>
                  )}
                  <div className="msg-bubble">{m.content}</div>
                  <div className="msg-meta">
                    {formatTime(m.sentAt)}
                    {m.status === 'sending' && <span className="msg-status"> · sending…</span>}
                    {m.status === 'failed' && (
                      <button
                        className="msg-retry"
                        onClick={() => m.tempId && onRetry(m.tempId)}
                      >
                        · failed — tap to retry
                      </button>
                    )}
                  </div>
                </div>
              );
            })}
            {messages.length === 0 && !loadingOlder && (
              <div className="muted center">No messages yet. Say hello!</div>
            )}
          </>
        )}
      </div>

      {shownTyping.length > 0 && !searchOpen && (
        <div className="typing-indicator">
          {shownTyping.join(', ')} {shownTyping.length === 1 ? 'is' : 'are'} typing…
        </div>
      )}

      <div className="composer">
        <input
          className="input"
          placeholder={`Message ${conversation.name ?? 'chat'}…`}
          value={draft}
          onChange={(e) => {
            setDraft(e.target.value);
            onTyping();
          }}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
              e.preventDefault();
              send();
            }
          }}
          onBlur={onTypingStopped}
        />
        <button className="btn btn-primary" onClick={send} disabled={!draft.trim()}>
          Send
        </button>
      </div>
    </main>
  );
}

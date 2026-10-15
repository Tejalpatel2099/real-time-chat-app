import { useCallback, useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { api } from '../lib/api';
import { createHubConnection } from '../lib/signalr';
import { useAuth } from '../auth/AuthContext';
import type {
  ChatMessage,
  ConversationDto,
  MessageDto,
  MessagePageDto,
  PresenceEvent,
  TypingEvent,
} from '../lib/types';
import ConversationSidebar from '../components/ConversationSidebar';
import MessagePane from '../components/MessagePane';
import NewConversationModal from '../components/NewConversationModal';

const PAGE_SIZE = 50;

export default function ChatPage() {
  const { user, logout } = useAuth();
  const [conversations, setConversations] = useState<ConversationDto[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [hasMore, setHasMore] = useState(false);
  const [loadingOlder, setLoadingOlder] = useState(false);
  const [onlineUsers, setOnlineUsers] = useState<Set<string>>(new Set());
  const [typingUsers, setTypingUsers] = useState<Map<string, string>>(new Map());
  const [showNewGroup, setShowNewGroup] = useState(false);
  const [connectionState, setConnectionState] = useState('Starting…');

  const hubRef = useRef<signalR.HubConnection | null>(null);
  const selectedIdRef = useRef<string | null>(null);
  const typingTimeouts = useRef(new Map<string, number>());
  const typingActive = useRef(false);
  const typingStopTimer = useRef<number | null>(null);

  selectedIdRef.current = selectedId;

  const patchConversationPreview = useCallback((msg: MessageDto) => {
    setConversations((prev) =>
      prev.map((c) =>
        c.id === msg.conversationId
          ? {
              ...c,
              lastMessage: {
                content: msg.content,
                sentAt: msg.sentAt,
                senderDisplayName: msg.senderDisplayName,
              },
            }
          : c,
      ),
    );
  }, []);

  // ---- Hub setup (once) ----
  useEffect(() => {
    const hub = createHubConnection();
    hubRef.current = hub;

    hub.on('ReceiveMessage', (msg: MessageDto) => {
      patchConversationPreview(msg);
      if (msg.conversationId !== selectedIdRef.current) return;
      setMessages((prev) => {
        if (prev.some((m) => m.id === msg.id)) return prev; // dedupe echo
        const pendingIdx = prev.findIndex(
          (m) => m.status === 'sending' && m.senderId === msg.senderId,
        );
        const confirmed: ChatMessage = { ...msg, status: 'sent' };
        if (pendingIdx >= 0) {
          const next = [...prev];
          next[pendingIdx] = confirmed;
          return next;
        }
        return [...prev, confirmed];
      });
    });

    hub.on('UserTyping', (ev: TypingEvent) => {
      if (ev.conversationId !== selectedIdRef.current) return;
      const existing = typingTimeouts.current.get(ev.userId);
      if (existing) window.clearTimeout(existing);
      if (ev.isTyping) {
        setTypingUsers((prev) => new Map(prev).set(ev.userId, ev.username));
        const timer = window.setTimeout(() => {
          setTypingUsers((prev) => {
            const next = new Map(prev);
            next.delete(ev.userId);
            return next;
          });
        }, 3000);
        typingTimeouts.current.set(ev.userId, timer);
      } else {
        setTypingUsers((prev) => {
          const next = new Map(prev);
          next.delete(ev.userId);
          return next;
        });
      }
    });

    hub.on('UserOnline', (ev: PresenceEvent) => {
      setOnlineUsers((prev) => new Set(prev).add(ev.userId));
    });
    hub.on('UserOffline', (ev: PresenceEvent) => {
      setOnlineUsers((prev) => {
        const next = new Set(prev);
        next.delete(ev.userId);
        return next;
      });
    });

    hub.onreconnecting(() => setConnectionState('Reconnecting…'));
    hub.onreconnected(async () => {
      setConnectionState('Connected');
      // Rejoin the open conversation: groups are per-connection.
      const id = selectedIdRef.current;
      if (id) {
        try {
          await hub.invoke('JoinConversation', id);
        } catch {
          /* membership re-check happens server-side */
        }
      }
    });
    hub.onclose(() => setConnectionState('Disconnected'));

    hub
      .start()
      .then(() => setConnectionState('Connected'))
      .catch(() => setConnectionState('Disconnected — retrying…'));

    api
      .get<string[]>('/presence/online')
      .then((res) => setOnlineUsers(new Set(res.data)))
      .catch(() => undefined);

    api
      .get<ConversationDto[]>('/conversations')
      .then((res) => {
        setConversations(res.data);
        if (res.data.length > 0) selectConversation(res.data[0].id);
      })
      .catch(() => undefined);

    return () => {
      typingTimeouts.current.forEach((t) => window.clearTimeout(t));
      hub.stop().catch(() => undefined);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const selectConversation = useCallback(
    async (id: string) => {
      const previous = selectedIdRef.current;
      setSelectedId(id);
      setMessages([]);
      setTypingUsers(new Map());
      setHasMore(false);
      setLoadingOlder(true);
      try {
        const hub = hubRef.current;
        if (hub && hub.state === signalR.HubConnectionState.Connected) {
          if (previous) {
            try {
              await hub.invoke('LeaveConversation', previous);
            } catch {
              /* best effort */
            }
          }
          await hub.invoke('JoinConversation', id);
        }
      } catch {
        /* hub join failure surfaces via connection banner */
      }
      try {
        const res = await api.get<MessagePageDto>('/messages', {
          params: { conversationId: id, limit: PAGE_SIZE },
        });
        setMessages(res.data.messages.map((m) => ({ ...m, status: 'sent' as const })));
        setHasMore(res.data.hasMore);
      } catch {
        /* keep the empty state; user can retry by reselecting */
      } finally {
        setLoadingOlder(false);
      }
    },
    [],
  );

  const loadOlder = useCallback(async () => {
    const id = selectedIdRef.current;
    if (!id || loadingOlder || !hasMore || messages.length === 0) return;
    setLoadingOlder(true);
    try {
      const res = await api.get<MessagePageDto>('/messages', {
        params: { conversationId: id, before: messages[0].sentAt, limit: PAGE_SIZE },
      });
      const older = res.data.messages.map((m) => ({ ...m, status: 'sent' as const }));
      // Guard against overlap if new messages arrived mid-request.
      setMessages((prev) => {
        const known = new Set(prev.map((m) => m.id));
        return [...older.filter((m) => !known.has(m.id)), ...prev];
      });
      setHasMore(res.data.hasMore);
    } finally {
      setLoadingOlder(false);
    }
  }, [loadingOlder, hasMore, messages]);

  const sendMessage = useCallback(
    async (text: string) => {
      const id = selectedIdRef.current;
      if (!id || !user) return;
      const tempId = `temp-${Date.now()}-${Math.random().toString(36).slice(2)}`;
      const optimistic: ChatMessage = {
        id: -Date.now(),
        conversationId: id,
        senderId: user.id,
        senderUsername: user.username,
        senderDisplayName: user.displayName,
        content: text,
        sentAt: new Date().toISOString(),
        status: 'sending',
        tempId,
      };
      setMessages((prev) => [...prev, optimistic]);

      // Primary path: SignalR hub persists + broadcasts (echo confirms).
      // Fallback: plain REST POST if the hub call fails.
      try {
        await hubRef.current?.invoke('SendMessage', id, text);
      } catch {
        try {
          const res = await api.post<MessageDto>('/messages', {
            conversationId: id,
            content: text,
          });
          const confirmed: ChatMessage = { ...res.data, status: 'sent' };
          patchConversationPreview(res.data);
          setMessages((prev) => prev.map((m) => (m.tempId === tempId ? confirmed : m)));
        } catch {
          setMessages((prev) =>
            prev.map((m) => (m.tempId === tempId ? { ...m, status: 'failed' as const } : m)),
          );
        }
      }
    },
    [user, patchConversationPreview],
  );

  const retryMessage = useCallback(
    (tempId: string) => {
      const failed = messages.find((m) => m.tempId === tempId);
      if (!failed) return;
      setMessages((prev) => prev.filter((m) => m.tempId !== tempId));
      void sendMessage(failed.content);
    },
    [messages, sendMessage],
  );

  const handleTyping = useCallback(() => {
    const hub = hubRef.current;
    const id = selectedIdRef.current;
    if (!hub || hub.state !== signalR.HubConnectionState.Connected || !id) return;
    if (!typingActive.current) {
      typingActive.current = true;
      hub.invoke('TypingStart', id).catch(() => undefined);
    }
    if (typingStopTimer.current) window.clearTimeout(typingStopTimer.current);
    typingStopTimer.current = window.setTimeout(() => {
      typingActive.current = false;
      hub.invoke('TypingStop', id).catch(() => undefined);
    }, 1200);
  }, []);

  const handleTypingStopped = useCallback(() => {
    const hub = hubRef.current;
    const id = selectedIdRef.current;
    if (typingStopTimer.current) window.clearTimeout(typingStopTimer.current);
    if (typingActive.current && hub && id) {
      typingActive.current = false;
      hub.invoke('TypingStop', id).catch(() => undefined);
    }
  }, []);

  const selected = conversations.find((c) => c.id === selectedId) ?? null;

  return (
    <div className="chat-layout">
      {connectionState !== 'Connected' && (
        <div className="conn-banner">{connectionState}</div>
      )}
      <ConversationSidebar
        conversations={conversations}
        selectedId={selectedId}
        onSelect={selectConversation}
        onlineUsers={onlineUsers}
        currentUserId={user?.id ?? ''}
        displayName={user?.displayName ?? ''}
        onNewGroup={() => setShowNewGroup(true)}
        onLogout={logout}
      />
      {selected && user ? (
        <MessagePane
          conversation={selected}
          messages={messages}
          hasMore={hasMore}
          loadingOlder={loadingOlder}
          onLoadOlder={loadOlder}
          onSend={sendMessage}
          onRetry={retryMessage}
          onTyping={handleTyping}
          onTypingStopped={handleTypingStopped}
          typingNames={[...typingUsers.values()]}
          onlineUsers={onlineUsers}
          currentUserId={user.id}
        />
      ) : (
        <main className="chat-main empty">
          <div className="muted">Select a conversation to start chatting.</div>
        </main>
      )}
      {showNewGroup && (
        <NewConversationModal
          onClose={() => setShowNewGroup(false)}
          onCreated={(c) => {
            setConversations((prev) => [c, ...prev]);
            void selectConversation(c.id);
          }}
        />
      )}
    </div>
  );
}

import { useState } from 'react';
import type { FormEvent } from 'react';
import { api } from '../lib/api';
import type { ConversationDto } from '../lib/types';

interface Props {
  onClose: () => void;
  onCreated: (c: ConversationDto) => void;
}

export default function NewConversationModal({ onClose, onCreated }: Props) {
  const [name, setName] = useState('');
  const [usernames, setUsernames] = useState('');
  const [isGroup, setIsGroup] = useState(true);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setBusy(true);
    try {
      const memberUsernames = usernames
        .split(',')
        .map((s) => s.trim())
        .filter(Boolean);
      const res = await api.post<ConversationDto>('/conversations', {
        name: name.trim() || null,
        isGroup,
        memberUsernames,
      });
      onCreated(res.data);
      onClose();
    } catch (err) {
      const msg =
        typeof err === 'object' && err !== null && 'response' in err
          ? (err as { response?: { data?: { error?: string } } }).response?.data?.error
          : undefined;
      setError(msg ?? 'Could not create the conversation.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <h2>New conversation</h2>
        <form onSubmit={submit}>
          <label>
            Name <span className="muted">(optional for 1:1 chats)</span>
            <input
              className="input"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. Weekend plans"
            />
          </label>
          <label>
            Usernames <span className="muted">(comma-separated)</span>
            <input
              className="input"
              value={usernames}
              onChange={(e) => setUsernames(e.target.value)}
              placeholder="e.g. demo1, demo2"
              required
            />
          </label>
          <label className="check-row">
            <input
              type="checkbox"
              checked={isGroup}
              onChange={(e) => setIsGroup(e.target.checked)}
            />
            Group chat
          </label>
          {error && <div className="error-text">{error}</div>}
          <div className="modal-actions">
            <button type="button" className="btn btn-ghost" onClick={onClose}>
              Cancel
            </button>
            <button type="submit" className="btn btn-primary" disabled={busy}>
              {busy ? 'Creating…' : 'Create'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

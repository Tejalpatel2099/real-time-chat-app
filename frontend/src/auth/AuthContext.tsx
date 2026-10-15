import { createContext, useCallback, useContext, useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { api } from '../lib/api';
import type { UserDto } from '../lib/types';

interface AuthContextValue {
  user: UserDto | null;
  loading: boolean;
  login: (usernameOrEmail: string, password: string) => Promise<void>;
  register: (username: string, email: string, password: string, displayName: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

function apiErrorMessage(err: unknown): string {
  if (typeof err === 'object' && err !== null && 'response' in err) {
    const data = (err as { response?: { data?: { error?: string } } }).response?.data;
    if (data?.error) return data.error;
  }
  return 'Something went wrong. Please try again.';
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const token = localStorage.getItem('chatapp_token');
    if (!token) {
      setLoading(false);
      return;
    }
    api
      .get<UserDto>('/auth/me')
      .then((res) => setUser(res.data))
      .catch(() => localStorage.removeItem('chatapp_token'))
      .finally(() => setLoading(false));
  }, []);

  const login = useCallback(async (usernameOrEmail: string, password: string) => {
    try {
      const res = await api.post<{ token: string; user: UserDto }>('/auth/login', {
        usernameOrEmail,
        password,
      });
      localStorage.setItem('chatapp_token', res.data.token);
      setUser(res.data.user);
    } catch (err) {
      throw new Error(apiErrorMessage(err));
    }
  }, []);

  const register = useCallback(
    async (username: string, email: string, password: string, displayName: string) => {
      try {
        await api.post('/auth/register', { username, email, password, displayName });
        // Auto-login after registration for a smooth first run.
        await login(username, password);
      } catch (err) {
        throw new Error(apiErrorMessage(err));
      }
    },
    [login],
  );

  const logout = useCallback(() => {
    localStorage.removeItem('chatapp_token');
    setUser(null);
  }, []);

  return (
    <AuthContext.Provider value={{ user, loading, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>');
  return ctx;
}

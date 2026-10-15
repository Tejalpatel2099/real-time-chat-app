import axios from 'axios';

/**
 * In `vite dev` this points at the API directly; in a production build it is
 * relative so nginx can proxy /api and /hubs to the API container.
 */
export const apiBase = import.meta.env.DEV ? 'http://localhost:5000' : '';

export const api = axios.create({
  baseURL: `${apiBase}/api`,
  headers: { 'Content-Type': 'application/json' },
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('chatapp_token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

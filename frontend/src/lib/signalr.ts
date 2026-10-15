import * as signalR from '@microsoft/signalr';
import { apiBase } from './api';

/**
 * Builds a SignalR connection with automatic reconnection (exponential
 * backoff: 0s, 2s, 10s, 30s, then every 30s). The JWT travels as
 * ?access_token= because browsers can't set an Authorization header on
 * the WebSocket handshake; the API reads it in JwtBearerEvents.
 */
export function createHubConnection(): signalR.HubConnection {
  return new signalR.HubConnectionBuilder()
    .withUrl(`${apiBase}/hubs/chat`, {
      accessTokenFactory: () => localStorage.getItem('chatapp_token') ?? '',
    })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Information)
    .build();
}

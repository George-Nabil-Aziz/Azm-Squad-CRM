import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { getAccessToken } from '../auth/session'
import type { NotificationPush } from './notifications'

/** Server hub path (server: NotificationsHub.Path). */
const hubPath = '/hubs/notifications'

/**
 * Opens the real-time notification connection (SignalR). The access token is sent as the `access_token` query string
 * (the SignalR client does this for WebSockets); the connection reconnects automatically. Returns a function that closes it.
 * Tests replace this module (src/test/setup.ts): no network is opened in tests.
 */
export function connectNotificationsHub(onNotification: (push: NotificationPush) => void): () => void {
  const connection = new HubConnectionBuilder()
    .withUrl(hubPath, { accessTokenFactory: () => getAccessToken() ?? '' })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.None)
    .build()
  connection.on('notification', onNotification)

  connection.start().catch(() => {
    // The server is unreachable or the token expired: the bell still works through the normal API calls.
  })

  return () => {
    void connection.stop()
  }
}

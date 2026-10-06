import { apiGet, apiPost } from './client'
import type { PagedResult } from './paging'

/** Types of notification the server sends (server: NotificationTypes). */
export type NotificationType = 'assignment' | 'slaWarning' | 'slaEscalation' | 'taskReminder' | 'mention'

/** One in-app notification (server: NotificationResponse). Times are UTC ISO 8601. */
export interface AppNotification {
  id: string
  type: NotificationType
  ticketId: string | null
  /** "TKT-000001", when the notification is about a ticket. */
  ticketNumber: string | null
  /** Escalation level for slaEscalation, otherwise 0. */
  level: number
  /** Free text (task title, mention excerpt). */
  text: string | null
  createdAt: string
  readAt: string | null
}

/** What the hub pushes for a new notification. */
export interface NotificationPush {
  notification: AppNotification
  unreadCount: number
}

/** GET /api/notifications: newest first. */
export function listNotifications(
  { unreadOnly = false, pageSize = 20 }: { unreadOnly?: boolean; pageSize?: number } = {},
  signal?: AbortSignal,
): Promise<PagedResult<AppNotification>> {
  const query = new URLSearchParams({ pageSize: String(pageSize) })
  if (unreadOnly) query.set('unreadOnly', 'true')
  return apiGet<PagedResult<AppNotification>>(`/api/notifications?${query.toString()}`, signal)
}

export function getUnreadCount(signal?: AbortSignal): Promise<{ count: number }> {
  return apiGet<{ count: number }>('/api/notifications/unread-count', signal)
}

export function markNotificationRead(id: string): Promise<AppNotification> {
  return apiPost<AppNotification>(`/api/notifications/${encodeURIComponent(id)}/read`, {})
}

export function markAllNotificationsRead(): Promise<{ count: number }> {
  return apiPost<{ count: number }>('/api/notifications/read-all', {})
}

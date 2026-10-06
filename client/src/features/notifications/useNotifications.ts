import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import type { TFunction } from 'i18next'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import {
  getUnreadCount,
  listNotifications,
  markAllNotificationsRead,
  markNotificationRead,
  type AppNotification,
} from '@/api/notifications'
import { connectNotificationsHub } from '@/api/notifications-hub'

/** Prefix of every notifications query. */
export const notificationsQueryKey = ['notifications'] as const
const unreadCountQueryKey = [...notificationsQueryKey, 'unread-count'] as const
const listQueryKey = [...notificationsQueryKey, 'list'] as const

/** "Ticket TKT-000001 was assigned to you.": the message of a notification in the UI language. */
export function notificationMessage(t: TFunction, notification: AppNotification): string {
  return t(`notifications.types.${notification.type}`, {
    ticket: notification.ticketNumber ?? '',
    level: notification.level,
    text: notification.text ?? '',
  })
}

/** The number of unread notifications (the bell badge). */
export function useUnreadCount() {
  return useQuery({
    queryKey: unreadCountQueryKey,
    queryFn: ({ signal }) => getUnreadCount(signal),
    select: (result) => result.count,
  })
}

/** The latest notifications, newest first; only loaded while the panel is open. */
export function useNotificationList(enabled: boolean) {
  return useQuery({
    queryKey: listQueryKey,
    queryFn: ({ signal }) => listNotifications({}, signal),
    enabled,
  })
}

/** Mark one or all notifications read; the list and the unread count reload. */
export function useMarkRead() {
  const queryClient = useQueryClient()
  const onSettled = () => queryClient.invalidateQueries({ queryKey: notificationsQueryKey })
  const one = useMutation({ mutationFn: (id: string) => markNotificationRead(id), onSettled })
  const all = useMutation({ mutationFn: () => markAllNotificationsRead(), onSettled })
  return { markRead: one.mutate, markAllRead: all.mutate, isPending: one.isPending || all.isPending }
}

/** Real time (SignalR): a pushed notification updates the unread count, reloads the list and shows a toast. */
export function useNotificationsRealtime(enabled: boolean) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  useEffect(() => {
    if (!enabled) return undefined
    return connectNotificationsHub(({ notification, unreadCount }) => {
      queryClient.setQueryData(unreadCountQueryKey, { count: unreadCount })
      void queryClient.invalidateQueries({ queryKey: listQueryKey })
      toast.info(notificationMessage(t, notification))
    })
  }, [enabled, queryClient, t])
}

import { BellIcon } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle, SheetTrigger } from '@/components/ui/sheet'
import { usePermissions } from '@/features/auth/usePermissions'
import {
  notificationMessage,
  useMarkRead,
  useNotificationList,
  useNotificationsRealtime,
  useUnreadCount,
} from './useNotifications'

/** Header bell: unread count, a panel with the latest notifications, mark as read; updates live through SignalR. */
export function NotificationBell() {
  const { can } = usePermissions()
  const allowed = can(permissions.notificationsView)
  const { t, i18n } = useTranslation()
  const [open, setOpen] = useState(false)
  const unread = useUnreadCount()
  const list = useNotificationList(open && allowed)
  const { markRead, markAllRead, isPending } = useMarkRead()
  useNotificationsRealtime(allowed)

  if (!allowed) return null

  const count = unread.data ?? 0
  const label = count > 0 ? t('notifications.openWithCount', { count }) : t('notifications.open')

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger asChild>
        <Button variant="outline" size="icon" aria-label={label} className="relative">
          <BellIcon aria-hidden="true" />
          {count > 0 ? (
            <span
              aria-hidden="true"
              className="absolute -top-1 -end-1 flex min-w-4 items-center justify-center rounded-full bg-destructive px-1 text-[10px] leading-4 font-medium text-white"
            >
              {count}
            </span>
          ) : null}
        </Button>
      </SheetTrigger>
      <SheetContent side="right" aria-describedby={undefined}>
        <SheetHeader>
          <SheetTitle>{t('notifications.title')}</SheetTitle>
          <SheetDescription className="sr-only">{t('notifications.description')}</SheetDescription>
        </SheetHeader>
        <div className="flex flex-col gap-3 overflow-y-auto px-4 pb-4">
          {count > 0 ? (
            <Button variant="outline" size="sm" className="self-start" disabled={isPending} onClick={() => markAllRead()}>
              {t('notifications.markAllRead')}
            </Button>
          ) : null}
          {list.isPending ? <p className="text-muted-foreground">{t('notifications.loading')}</p> : null}
          {list.data?.items.length === 0 ? <p className="text-muted-foreground">{t('notifications.empty')}</p> : null}
          <ul className="flex flex-col gap-2">
            {list.data?.items.map((notification) => {
              const message = notificationMessage(t, notification)
              const unreadItem = notification.readAt === null
              return (
                <li
                  key={notification.id}
                  className={`flex flex-col gap-1 rounded-md border p-3 ${unreadItem ? 'bg-accent' : ''}`}
                >
                  {notification.ticketId ? (
                    <Link to={`/tickets/${notification.ticketId}`} onClick={() => setOpen(false)} className="font-medium underline-offset-2 hover:underline">
                      {message}
                    </Link>
                  ) : (
                    <span className="font-medium">{message}</span>
                  )}
                  <div className="flex items-center justify-between gap-2">
                    <time dateTime={notification.createdAt} className="text-xs text-muted-foreground">
                      {new Date(notification.createdAt).toLocaleString(i18n.language)}
                    </time>
                    {unreadItem ? (
                      <Button variant="ghost" size="sm" disabled={isPending} onClick={() => markRead(notification.id)}>
                        {t('notifications.markRead')}
                      </Button>
                    ) : null}
                  </div>
                </li>
              )
            })}
          </ul>
        </div>
      </SheetContent>
    </Sheet>
  )
}

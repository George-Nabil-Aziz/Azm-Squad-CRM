import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { permissions } from '@/auth/permissions'
import { Card, CardContent } from '@/components/ui/card'
import { usePermissions } from '@/features/auth/usePermissions'
import { useUnreadCount } from '@/features/notifications/useNotifications'
import { DashboardSection, SectionError, SectionSkeleton } from './DashboardSection'
import { MyTickets } from './MyTickets'
import { StatCard } from './StatCard'
import { useOpenTasks } from './useDashboardData'

const TASKS_SHOWN = 5

function MyTasks() {
  const { t, i18n } = useTranslation()
  const tasks = useOpenTasks()
  const format = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' })

  return (
    <DashboardSection
      id="my-tasks-title"
      level={3}
      title={t('dashboard.myWork.tasks.title')}
      action={{ label: t('dashboard.myWork.tasks.all'), to: '/tasks' }}
    >
      {tasks.isPending ? <SectionSkeleton rows={2} label={t('dashboard.loading')} /> : null}
      {tasks.isError ? <SectionError message={t('dashboard.myWork.tasks.loadError')} /> : null}
      {tasks.data && tasks.data.length === 0 ? <p className="text-muted-foreground">{t('dashboard.myWork.tasks.empty')}</p> : null}
      {tasks.data && tasks.data.length > 0 ? (
        <Card>
          <CardContent>
            <ul className="flex flex-col divide-y">
              {tasks.data.slice(0, TASKS_SHOWN).map((task) => (
                <li key={task.id} className="flex flex-wrap items-center justify-between gap-2 py-2 first:pt-0 last:pb-0">
                  <span className="font-medium">{task.title}</span>
                  <span className="text-sm text-muted-foreground">
                    {task.ticketNumber ? (
                      <Link to={`/tickets/${task.ticketId}`} dir="ltr" className="me-2 text-primary underline-offset-4 hover:underline">
                        {task.ticketNumber}
                      </Link>
                    ) : null}
                    {t('dashboard.myWork.tasks.due', { date: format.format(new Date(task.dueAt)) })}
                  </span>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      ) : null}
    </DashboardSection>
  )
}

function UnreadNotifications() {
  const { t } = useTranslation()
  const unread = useUnreadCount()
  return <StatCard title={t('dashboard.myWork.unread')} value={unread.isError ? '–' : unread.data} />
}

/** Everything that is mine: my tickets and counters, my tasks due soon, my unread notifications. */
export function MyWork() {
  const { t } = useTranslation()
  const { can } = usePermissions()
  const showTickets = can(permissions.ticketsView)
  const showTasks = can(permissions.tasksManage)
  const showNotifications = can(permissions.notificationsView)
  if (!showTickets && !showTasks && !showNotifications) return null

  return (
    <DashboardSection id="my-work-title" title={t('dashboard.sections.myWork')}>
      <div className="grid gap-6 lg:grid-cols-3">
        <div className="flex flex-col gap-6 lg:col-span-2">{showTickets ? <MyTickets /> : null}</div>
        <div className="flex flex-col gap-6">
          {showNotifications ? <UnreadNotifications /> : null}
          {showTasks ? <MyTasks /> : null}
        </div>
      </div>
    </DashboardSection>
  )
}

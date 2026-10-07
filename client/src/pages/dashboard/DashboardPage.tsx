import { useTranslation } from 'react-i18next'
import { permissions } from '@/auth/permissions'
import { Can } from '@/features/auth/Can'
import { useCurrentUser } from '@/features/auth/useCurrentUser'
import { Charts } from '@/features/dashboard/Charts'
import { MyWork } from '@/features/dashboard/MyWork'
import { Operations } from '@/features/dashboard/Operations'
import { Performance } from '@/features/dashboard/Performance'
import { OverdueTickets, RecentTickets } from '@/features/dashboard/RecentTickets'
import { SystemOverview } from '@/features/dashboard/SystemOverview'
import { Team } from '@/features/dashboard/Team'

/**
 * Staff home: my work for everyone, then (by permission) the operations overview, performance, charts,
 * recent and overdue tickets and the top agents. The API checks every permission itself; this only hides sections.
 */
export function DashboardPage() {
  const { t } = useTranslation()
  const { data: user } = useCurrentUser()

  return (
    <div className="flex flex-col gap-8">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold">{t('nav.dashboard')}</h1>
        {user ? <p className="text-muted-foreground">{t('dashboard.welcome', { name: user.fullName })}</p> : null}
      </div>
      <MyWork />
      <Can permission={permissions.ticketsView}>
        <Operations />
      </Can>
      <Can permission={permissions.reportsView}>
        <Performance />
        <Charts />
      </Can>
      <Can permission={permissions.ticketsView}>
        <div className="grid gap-6 lg:grid-cols-2">
          <RecentTickets />
          <OverdueTickets />
        </div>
      </Can>
      <Can permission={permissions.usersManage}>
        <SystemOverview />
      </Can>
      <Can permission={permissions.reportsView}>
        <Team />
      </Can>
    </div>
  )
}

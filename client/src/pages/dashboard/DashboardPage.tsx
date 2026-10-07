import { useTranslation } from 'react-i18next'
import { permissions } from '@/auth/permissions'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
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
 * Staff home in two tabs: the whole team's work (default tab "all"; by permission the operations overview,
 * performance, charts, recent and overdue tickets, system overview and top agents) and the signed-in user's own
 * work (tab "mine": my tickets, tasks and notifications).
 * The API checks every permission itself; this only hides sections.
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
      <Tabs defaultValue="all" className="gap-6">
        <TabsList>
          <TabsTrigger value="all">{t('dashboard.tabs.all')}</TabsTrigger>
          <TabsTrigger value="mine">{t('dashboard.tabs.mine')}</TabsTrigger>
        </TabsList>
        {/* Both panels stay mounted (hidden when inactive) so switching tabs keeps their loaded data; divide-y draws a line between sections. */}
        <TabsContent value="all" forceMount className="flex flex-col divide-y divide-border [&>*]:py-8 [&>*:first-child]:pt-0 [&>*:last-child]:pb-0 data-[state=inactive]:hidden">
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
        </TabsContent>
        <TabsContent value="mine" forceMount className="flex flex-col divide-y divide-border [&>*]:py-8 [&>*:first-child]:pt-0 [&>*:last-child]:pb-0 data-[state=inactive]:hidden">
          <MyWork />
        </TabsContent>
      </Tabs>
    </div>
  )
}

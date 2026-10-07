import { useTranslation } from 'react-i18next'
import { permissions } from '@/auth/permissions'
import { Can } from '@/features/auth/Can'
import { useCurrentUser } from '@/features/auth/useCurrentUser'
import { MyTickets } from '@/features/dashboard/MyTickets'

/** Dashboard: welcome line, my assigned tickets with counters (CRM-29). Report widgets come with the reports stories. */
export function DashboardPage() {
  const { t } = useTranslation()
  const { data: user } = useCurrentUser()

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold">{t('nav.dashboard')}</h1>
        {user ? <p className="text-muted-foreground">{t('dashboard.welcome', { name: user.fullName })}</p> : null}
      </div>
      <Can permission={permissions.ticketsView}>
        <MyTickets />
      </Can>
    </div>
  )
}

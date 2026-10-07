import { CircleCheckIcon, HourglassIcon, InboxIcon, SparklesIcon, TimerOffIcon, UsersIcon } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { permissions } from '@/auth/permissions'
import { usePermissions } from '@/features/auth/usePermissions'
import { DashboardSection, SectionError } from './DashboardSection'
import { StatCard } from './StatCard'
import { useDashboardOverview } from './useDashboardData'

/** Counts of all tickets (and customers): the staff view of the whole desk. */
export function Operations() {
  const { t } = useTranslation()
  const { can } = usePermissions()
  const overview = useDashboardOverview()
  const data = overview.data
  const [loadedAt] = useState(() => new Date().toISOString())
  const today = (data?.generatedAt ?? loadedAt).slice(0, 10)
  const breachedLink = can(permissions.reportsView) ? '/reports/sla' : '/tickets'

  return (
    <DashboardSection id="operations-title" title={t('dashboard.sections.operations')}>
      {overview.isError ? (
        <SectionError message={t('dashboard.operations.loadError')} />
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
          <StatCard title={t('dashboard.operations.openAll')} icon={InboxIcon} accent="indigo" value={data?.openTickets} to="/tickets?status=open" />
          <StatCard title={t('dashboard.operations.pendingAll')} icon={HourglassIcon} accent="amber" value={data?.pendingTickets} to="/tickets?status=pending" />
          <StatCard
            title={t('dashboard.operations.breachedNow')}
            icon={TimerOffIcon}
            accent="rose"
            value={data?.breachedNow}
            to={breachedLink}
            tone={(data?.breachedNow ?? 0) > 0 ? 'danger' : 'default'}
          />
          <StatCard title={t('dashboard.operations.resolvedToday')} icon={CircleCheckIcon} accent="emerald" value={data?.resolvedToday} to="/tickets?status=resolved" />
          <StatCard title={t('dashboard.operations.newToday')} icon={SparklesIcon} accent="sky" value={data?.newToday} to={`/tickets?createdFrom=${today}`} />
          {can(permissions.customersView) && (data === undefined || data.totalCustomers !== null) ? (
            <StatCard title={t('dashboard.operations.customers')} icon={UsersIcon} accent="violet" value={data?.totalCustomers ?? undefined} to="/customers" />
          ) : null}
        </div>
      )}
    </DashboardSection>
  )
}

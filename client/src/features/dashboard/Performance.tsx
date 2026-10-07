import { useTranslation } from 'react-i18next'
import { useDashboard } from '@/features/reports/useDashboard'
import { useSlaReport } from '@/features/reports/useSlaReport'
import { formatMinutes } from '@/features/sla/sla-format'
import { DashboardSection, SectionError } from './DashboardSection'
import { slaCompliancePercent } from './sla-compliance'
import { StatCard } from './StatCard'

const NONE = '–'

/** SLA compliance, average first response and resolution, average CSAT of the last 30 days. */
export function Performance() {
  const { t } = useTranslation()
  const sla = useSlaReport({})
  const dashboard = useDashboard()
  const minutes = (value: number | null | undefined) =>
    value === undefined ? undefined : value === null ? NONE : formatMinutes(Math.round(value), t)
  const compliance = sla.data ? slaCompliancePercent(sla.data.overall.response, sla.data.overall.resolution) : undefined
  const last30 = t('dashboard.performance.last30')

  return (
    <DashboardSection id="performance-title" title={t('dashboard.sections.performance')}>
      {sla.isError && dashboard.isError ? <SectionError message={t('dashboard.performance.loadError')} /> : null}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard
          title={t('dashboard.performance.slaCompliance')}
          value={compliance === undefined ? undefined : compliance === null ? NONE : `${compliance}%`}
          hint={last30}
          to="/reports/sla"
        />
        <StatCard
          title={t('dashboard.performance.avgResponse')}
          value={minutes(dashboard.data?.averageResponseMinutes)}
          hint={last30}
          to="/reports/sla"
        />
        <StatCard
          title={t('dashboard.performance.avgResolution')}
          value={minutes(sla.data?.overall.resolution.averageMinutes)}
          hint={last30}
          to="/reports/sla"
        />
        <StatCard
          title={t('dashboard.performance.avgCsat')}
          value={dashboard.data ? (dashboard.data.averageCsat === null ? NONE : String(dashboard.data.averageCsat)) : undefined}
          hint={dashboard.data ? t('reports.csat.ratings', { count: dashboard.data.csatCount }) : undefined}
          to="/reports/satisfaction"
        />
      </div>
    </DashboardSection>
  )
}

import { useTranslation } from 'react-i18next'
import { Bar, BarChart, CartesianGrid, XAxis, YAxis } from 'recharts'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { ChartContainer, ChartTooltip, ChartTooltipContent, type ChartConfig } from '@/components/ui/chart'
import { useDashboard } from '@/features/reports/useDashboard'
import { formatMinutes } from '@/features/sla/sla-format'

const NONE = '–'

const chartConfig = { count: { label: 'count', color: 'var(--chart-1)' } } satisfies ChartConfig

interface KpiProps {
  title: string
  value: string
  hint?: string
}

function Kpi({ title, value, hint }: KpiProps) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-1">
        <p className="text-4xl font-semibold tabular-nums">{value}</p>
        {hint ? <p className="text-sm text-muted-foreground">{hint}</p> : null}
      </CardContent>
    </Card>
  )
}

/** Live management dashboard: KPI cards and charts of tickets per day and by channel; reloads itself every 30 seconds. */
export function DashboardReportPage() {
  const { t, i18n } = useTranslation()
  const dashboard = useDashboard()
  const data = dashboard.data
  const formatClock = new Intl.DateTimeFormat(i18n.language, { timeStyle: 'medium' })

  if (!data) {
    return <p className="text-muted-foreground">{t('reports.loading')}</p>
  }

  const perDay = data.ticketsPerDay.map((day) => ({ label: day.date.slice(5), count: day.count }))
  const byChannel = data.ticketsByChannel.map((channel) => ({ label: t(`tickets.channels.${channel.key}`), count: channel.count }))

  return (
    <div className="flex flex-col gap-6">
      <p className="text-sm text-muted-foreground">{t('reports.dashboard.updated', { time: formatClock.format(new Date(data.generatedAt)) })}</p>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Kpi title={t('reports.dashboard.open')} value={String(data.openTickets)} />
        <Kpi title={t('reports.dashboard.breachedToday')} value={String(data.breachedToday)} />
        <Kpi
          title={t('reports.dashboard.avgResponse')}
          value={data.averageResponseMinutes === null ? NONE : formatMinutes(Math.round(data.averageResponseMinutes), t)}
          hint={t('reports.dashboard.last30')}
        />
        <Kpi
          title={t('reports.dashboard.avgCsat')}
          value={data.averageCsat === null ? NONE : String(data.averageCsat)}
          hint={t('reports.csat.ratings', { count: data.csatCount })}
        />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>{t('reports.dashboard.perDay')}</CardTitle>
          </CardHeader>
          <CardContent>
            <ChartContainer config={chartConfig} className="h-64 w-full" role="img" aria-label={t('reports.dashboard.perDay')}>
              <BarChart data={perDay}>
                <CartesianGrid vertical={false} />
                <XAxis dataKey="label" tickLine={false} axisLine={false} />
                <YAxis allowDecimals={false} width={32} />
                <ChartTooltip content={<ChartTooltipContent />} />
                <Bar dataKey="count" fill="var(--color-count)" radius={4} />
              </BarChart>
            </ChartContainer>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>{t('reports.dashboard.byChannel')}</CardTitle>
          </CardHeader>
          <CardContent>
            <ChartContainer config={chartConfig} className="h-64 w-full" role="img" aria-label={t('reports.dashboard.byChannel')}>
              <BarChart data={byChannel}>
                <CartesianGrid vertical={false} />
                <XAxis dataKey="label" tickLine={false} axisLine={false} />
                <YAxis allowDecimals={false} width={32} />
                <ChartTooltip content={<ChartTooltipContent />} />
                <Bar dataKey="count" fill="var(--color-count)" radius={4} />
              </BarChart>
            </ChartContainer>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}

import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Bar, BarChart, CartesianGrid, Cell, XAxis, YAxis } from 'recharts'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { ChartContainer, ChartTooltip, ChartTooltipContent, type ChartConfig } from '@/components/ui/chart'
import { useTicketReport } from '@/features/reports/useTicketReport'
import { DashboardSection, SectionError, SectionSkeleton } from './DashboardSection'
import { utcDay } from './useDashboardData'

const chartConfig = { count: { label: 'count', color: 'var(--chart-1)' } } satisfies ChartConfig
const palette = ['var(--chart-1)', 'var(--chart-2)', 'var(--chart-3)', 'var(--chart-4)', 'var(--chart-5)']
const RANGES = [14, 30] as const

interface ChartCardProps {
  title: string
  data: { label: string; count: number }[]
  colored?: boolean
}

function ChartCard({ title, data, colored }: ChartCardProps) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
      </CardHeader>
      <CardContent>
        <ChartContainer config={chartConfig} className="h-56 w-full" role="img" aria-label={title}>
          <BarChart data={data}>
            <CartesianGrid vertical={false} />
            <XAxis dataKey="label" tickLine={false} axisLine={false} interval="preserveStartEnd" />
            <YAxis allowDecimals={false} width={32} />
            <ChartTooltip content={<ChartTooltipContent />} />
            <Bar dataKey="count" fill="var(--color-count)" radius={4}>
              {colored ? data.map((entry, index) => <Cell key={entry.label} fill={palette[index % palette.length]} />) : null}
            </Bar>
          </BarChart>
        </ChartContainer>
      </CardContent>
    </Card>
  )
}

/** Tickets per day (14 or 30 days), by channel and by status of the same range; the data of the ticket report. */
export function Charts() {
  const { t } = useTranslation()
  const [days, setDays] = useState<(typeof RANGES)[number]>(14)
  const [now] = useState(() => new Date())
  const range = useMemo(() => ({ from: utcDay(now, days - 1), to: utcDay(now) }), [now, days])
  const report = useTicketReport(range)
  const data = report.data

  return (
    <DashboardSection
      id="charts-title"
      title={t('dashboard.sections.charts')}
      action={{ label: t('dashboard.charts.reportLink'), to: '/reports/tickets' }}
    >
      <div role="group" aria-label={t('dashboard.charts.range')} className="flex gap-2">
        {RANGES.map((value) => (
          <Button key={value} size="sm" variant={days === value ? 'default' : 'outline'} aria-pressed={days === value} onClick={() => setDays(value)}>
            {t(`dashboard.charts.days${value}`)}
          </Button>
        ))}
      </div>
      {report.isError ? <SectionError message={t('dashboard.charts.loadError')} /> : null}
      {report.isPending ? <SectionSkeleton rows={4} label={t('dashboard.loading')} /> : null}
      {data ? (
        <div className="grid gap-4 lg:grid-cols-3">
          <div className="lg:col-span-3">
            <ChartCard title={t('dashboard.charts.perDay')} data={data.byDay.map((day) => ({ label: day.date.slice(5), count: day.count }))} />
          </div>
          <div className="lg:col-span-2">
            <ChartCard
              title={t('dashboard.charts.byChannel')}
              colored
              data={data.byChannel.map((row) => ({ label: t(`tickets.channels.${row.key}`), count: row.count }))}
            />
          </div>
          <ChartCard
            title={t('dashboard.charts.byStatus')}
            colored
            data={data.byStatus.map((row) => ({ label: t(`tickets.statuses.${row.key}`), count: row.count }))}
          />
        </div>
      ) : null}
    </DashboardSection>
  )
}

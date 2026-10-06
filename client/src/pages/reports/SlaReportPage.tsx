import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import type { SlaPriorityRow, SlaTargetStats } from '@/api/reports'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { DateRangeFields } from '@/features/reports/DateRangeFields'
import { useSlaBreaches, useSlaReport } from '@/features/reports/useSlaReport'
import { formatMinutes } from '@/features/sla/sla-format'

const PAGE_SIZE = 20
const NONE = '–'

/** SLA performance: compliance % and average times per priority, and the breached tickets (each links to the ticket). */
export function SlaReportPage() {
  const { t, i18n } = useTranslation()
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [page, setPage] = useState(1)
  const range = { ...(from ? { from } : {}), ...(to ? { to } : {}) }
  const report = useSlaReport(range)
  const breaches = useSlaBreaches({ ...range, page, pageSize: PAGE_SIZE })
  const formatTime = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' })
  const totalPages = breaches.data ? Math.max(1, Math.ceil(breaches.data.totalCount / breaches.data.pageSize)) : 1

  const percent = (stats: SlaTargetStats) => (stats.compliancePercent === null ? NONE : `${stats.compliancePercent}%`)
  const average = (stats: SlaTargetStats) => (stats.averageMinutes === null ? NONE : formatMinutes(Math.round(stats.averageMinutes), t))

  function renderRow(row: SlaPriorityRow) {
    return (
      <TableRow key={row.priority}>
        <TableCell className="font-medium">{row.priority === 'all' ? t('reports.sla.all') : t(`tickets.priorities.${row.priority}`)}</TableCell>
        <TableCell className="tabular-nums">{row.tickets}</TableCell>
        <TableCell className="tabular-nums">{percent(row.response)}</TableCell>
        <TableCell className="tabular-nums">{average(row.response)}</TableCell>
        <TableCell className="tabular-nums">{percent(row.resolution)}</TableCell>
        <TableCell className="tabular-nums">{average(row.resolution)}</TableCell>
      </TableRow>
    )
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="grid items-end gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <DateRangeFields
          from={from}
          to={to}
          onFromChange={(value) => {
            setFrom(value)
            setPage(1)
          }}
          onToChange={(value) => {
            setTo(value)
            setPage(1)
          }}
        />
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{t('reports.sla.title')}</CardTitle>
        </CardHeader>
        <CardContent>
          {report.data ? (
            <>
              <p className="mb-3 text-sm text-muted-foreground">{t('reports.sla.range', { from: report.data.from, to: report.data.to })}</p>
              <Table aria-label={t('reports.sla.title')}>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('reports.filters.priority')}</TableHead>
                    <TableHead>{t('reports.count')}</TableHead>
                    <TableHead>{t('reports.sla.responseMet')}</TableHead>
                    <TableHead>{t('reports.sla.avgResponse')}</TableHead>
                    <TableHead>{t('reports.sla.resolutionMet')}</TableHead>
                    <TableHead>{t('reports.sla.avgResolution')}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {report.data.priorities.map(renderRow)}
                  {renderRow(report.data.overall)}
                </TableBody>
              </Table>
            </>
          ) : (
            <p className="text-muted-foreground">{t('reports.loading')}</p>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t('reports.sla.breachedTitle')}</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          {breaches.data && breaches.data.items.length > 0 ? (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('reports.sla.ticket')}</TableHead>
                  <TableHead>{t('reports.sla.subject')}</TableHead>
                  <TableHead>{t('reports.filters.priority')}</TableHead>
                  <TableHead>{t('reports.sla.assignee')}</TableHead>
                  <TableHead>{t('reports.sla.created')}</TableHead>
                  <TableHead>{t('reports.sla.missed')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {breaches.data.items.map((ticket) => (
                  <TableRow key={ticket.ticketId}>
                    <TableCell>
                      <Link className="underline" to={`/tickets/${ticket.ticketId}`}>
                        {ticket.number}
                      </Link>
                    </TableCell>
                    <TableCell>{ticket.subject}</TableCell>
                    <TableCell>{t(`tickets.priorities.${ticket.priority}`)}</TableCell>
                    <TableCell>{ticket.assigneeName ?? NONE}</TableCell>
                    <TableCell className="whitespace-nowrap">{formatTime.format(new Date(ticket.createdAt))}</TableCell>
                    <TableCell>
                      {[
                        ticket.responseBreached ? t('reports.sla.targetResponse') : null,
                        ticket.resolutionBreached ? t('reports.sla.targetResolution') : null,
                      ]
                        .filter(Boolean)
                        .join(', ')}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          ) : breaches.data ? (
            <p className="text-muted-foreground">{t('reports.sla.noBreaches')}</p>
          ) : (
            <p className="text-muted-foreground">{t('reports.loading')}</p>
          )}
          <div className="flex items-center justify-end gap-2">
            <span className="text-sm text-muted-foreground">{t('reports.pageInfo', { page, pages: totalPages })}</span>
            <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
              {t('reports.previous')}
            </Button>
            <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
              {t('reports.next')}
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}

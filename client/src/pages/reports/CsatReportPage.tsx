import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { DateRangeFields } from '@/features/reports/DateRangeFields'
import { useCsatReport } from '@/features/reports/useCsatReport'

const NONE = '–'

/** Customer satisfaction: average and 1-5 distribution, trend, by agent / category, low ratings and the response rate. */
export function CsatReportPage() {
  const { t, i18n } = useTranslation()
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const report = useCsatReport({ ...(from ? { from } : {}), ...(to ? { to } : {}) })
  const formatTime = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' })
  const data = report.data

  return (
    <div className="flex flex-col gap-6">
      <div className="grid items-end gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <DateRangeFields from={from} to={to} onFromChange={setFrom} onToChange={setTo} />
      </div>

      {!data ? (
        <p className="text-muted-foreground">{t('reports.loading')}</p>
      ) : (
        <>
          <div className="grid gap-4 sm:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle>{t('reports.csat.average')}</CardTitle>
              </CardHeader>
              <CardContent className="flex flex-col gap-1">
                <p className="text-4xl font-semibold tabular-nums">{data.averageRating ?? NONE}</p>
                <p className="text-sm text-muted-foreground">{t('reports.csat.ratings', { count: data.totalRatings })}</p>
              </CardContent>
            </Card>
            <Card>
              <CardHeader>
                <CardTitle>{t('reports.csat.responseRate')}</CardTitle>
              </CardHeader>
              <CardContent className="flex flex-col gap-1">
                <p className="text-4xl font-semibold tabular-nums">
                  {data.responseRatePercent === null ? NONE : `${data.responseRatePercent}%`}
                </p>
                <p className="text-sm text-muted-foreground">
                  {t('reports.csat.answered', { ratings: data.totalRatings, sent: data.surveysSent })}
                </p>
              </CardContent>
            </Card>
          </div>

          <div className="grid gap-4 lg:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle>{t('reports.csat.distribution')}</CardTitle>
              </CardHeader>
              <CardContent>
                <Table aria-label={t('reports.csat.distribution')}>
                  <TableHeader>
                    <TableRow>
                      <TableHead>{t('reports.csat.rating')}</TableHead>
                      <TableHead className="text-end">{t('reports.count')}</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {data.distribution.map((row) => (
                      <TableRow key={row.rating}>
                        <TableCell>{t('reports.csat.stars', { count: row.rating })}</TableCell>
                        <TableCell className="text-end tabular-nums">{row.count}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
            <Card>
              <CardHeader>
                <CardTitle>{t('reports.csat.trend')}</CardTitle>
              </CardHeader>
              <CardContent>
                <Table aria-label={t('reports.csat.trend')}>
                  <TableHeader>
                    <TableRow>
                      <TableHead>{t('reports.tickets.day')}</TableHead>
                      <TableHead className="text-end">{t('reports.csat.averageShort')}</TableHead>
                      <TableHead className="text-end">{t('reports.count')}</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {data.byDay.map((day) => (
                      <TableRow key={day.date}>
                        <TableCell>{day.date}</TableCell>
                        <TableCell className="text-end tabular-nums">{day.averageRating ?? NONE}</TableCell>
                        <TableCell className="text-end tabular-nums">{day.count}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
            <GroupTable title={t('reports.csat.byAgent')} fallback={t('reports.csat.noAgent')} groups={data.byAgent} />
            <GroupTable title={t('reports.csat.byCategory')} fallback={t('reports.tickets.uncategorized')} groups={data.byCategory} />
          </div>

          <Card>
            <CardHeader>
              <CardTitle>{t('reports.csat.low')}</CardTitle>
            </CardHeader>
            <CardContent>
              {data.lowRatings.length === 0 ? (
                <p className="text-muted-foreground">{t('reports.csat.noLow')}</p>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>{t('reports.sla.ticket')}</TableHead>
                      <TableHead>{t('reports.csat.rating')}</TableHead>
                      <TableHead>{t('reports.csat.comment')}</TableHead>
                      <TableHead>{t('reports.sla.assignee')}</TableHead>
                      <TableHead>{t('reports.csat.date')}</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {data.lowRatings.map((low) => (
                      <TableRow key={`${low.ticketId}-${low.ratedAt}`}>
                        <TableCell>
                          <Link className="underline" to={`/tickets/${low.ticketId}`}>
                            {low.ticketNumber}
                          </Link>
                        </TableCell>
                        <TableCell className="tabular-nums">{low.rating}</TableCell>
                        <TableCell>{low.comment ?? NONE}</TableCell>
                        <TableCell>{low.agentName ?? NONE}</TableCell>
                        <TableCell className="whitespace-nowrap">{formatTime.format(new Date(low.ratedAt))}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </>
      )}
    </div>
  )
}

interface GroupTableProps {
  title: string
  /** Label of the bucket without an id (no agent / no category). */
  fallback: string
  groups: { id: string | null; name: string | null; averageRating: number; count: number }[]
}

function GroupTable({ title, fallback, groups }: GroupTableProps) {
  const { t } = useTranslation()

  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
      </CardHeader>
      <CardContent>
        <Table aria-label={title}>
          <TableHeader>
            <TableRow>
              <TableHead>{title}</TableHead>
              <TableHead className="text-end">{t('reports.csat.averageShort')}</TableHead>
              <TableHead className="text-end">{t('reports.count')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {groups.map((group) => (
              <TableRow key={group.id ?? 'none'}>
                <TableCell>{group.name ?? fallback}</TableCell>
                <TableCell className="text-end tabular-nums">{group.averageRating}</TableCell>
                <TableCell className="text-end tabular-nums">{group.count}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  )
}

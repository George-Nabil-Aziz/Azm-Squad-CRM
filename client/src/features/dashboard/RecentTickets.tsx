import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { permissions } from '@/auth/permissions'
import { Card, CardContent } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { usePermissions } from '@/features/auth/usePermissions'
import { DashboardSection, SectionError, SectionSkeleton } from './DashboardSection'
import { TicketBadges } from './TicketBadges'
import { useDashboardOverview, useRecentTickets } from './useDashboardData'

const RECENT_COUNT = 6

/** The latest tickets with status and priority. */
export function RecentTickets() {
  const { t } = useTranslation()
  const recent = useRecentTickets(RECENT_COUNT)

  return (
    <DashboardSection
      id="recent-title"
      level={3}
      title={t('dashboard.sections.recent')}
      action={{ label: t('dashboard.recent.viewAll'), to: '/tickets' }}
    >
      {recent.isPending ? <SectionSkeleton label={t('dashboard.loading')} /> : null}
      {recent.isError ? <SectionError message={t('dashboard.recent.loadError')} /> : null}
      {recent.data && recent.data.items.length === 0 ? <p className="text-muted-foreground">{t('dashboard.recent.empty')}</p> : null}
      {recent.data && recent.data.items.length > 0 ? (
        <Card>
          <CardContent className="px-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('tickets.columns.number')}</TableHead>
                  <TableHead>{t('tickets.columns.subject')}</TableHead>
                  <TableHead>{t('tickets.columns.status')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {recent.data.items.map((ticket) => (
                  <TableRow key={ticket.id}>
                    <TableCell dir="ltr" className="text-start font-medium">
                      <Link to={`/tickets/${ticket.id}`} className="text-primary underline-offset-4 hover:underline">
                        {ticket.number}
                      </Link>
                    </TableCell>
                    <TableCell className="max-w-48 truncate">{ticket.subject}</TableCell>
                    <TableCell>
                      <TicketBadges status={ticket.status} priority={ticket.priority} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      ) : null}
    </DashboardSection>
  )
}

/** Open tickets past an SLA due time, the longest overdue first. */
export function OverdueTickets() {
  const { t, i18n } = useTranslation()
  const { can } = usePermissions()
  const overview = useDashboardOverview()
  const format = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' })

  return (
    <DashboardSection
      id="overdue-title"
      level={3}
      title={t('dashboard.sections.overdue')}
      action={can(permissions.reportsView) ? { label: t('dashboard.overdue.viewAll'), to: '/reports/sla' } : undefined}
    >
      {overview.isPending ? <SectionSkeleton label={t('dashboard.loading')} /> : null}
      {overview.isError ? <SectionError message={t('dashboard.overdue.loadError')} /> : null}
      {overview.data && overview.data.overdue.length === 0 ? <p className="text-muted-foreground">{t('dashboard.overdue.empty')}</p> : null}
      {overview.data && overview.data.overdue.length > 0 ? (
        <Card>
          <CardContent>
            <ul className="flex flex-col divide-y">
              {overview.data.overdue.map((ticket) => (
                <li key={ticket.ticketId} className="flex flex-col gap-1 py-2 first:pt-0 last:pb-0">
                  <div className="flex items-center justify-between gap-2">
                    <Link to={`/tickets/${ticket.ticketId}`} dir="ltr" className="font-medium text-primary underline-offset-4 hover:underline">
                      {ticket.number}
                    </Link>
                    <TicketBadges priority={ticket.priority} />
                  </div>
                  <span className="truncate">{ticket.subject}</span>
                  <span className="text-sm text-muted-foreground">
                    {ticket.assigneeName ?? t('dashboard.overdue.unassigned')} · {t('dashboard.overdue.due', { date: format.format(new Date(ticket.dueAt)) })}
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

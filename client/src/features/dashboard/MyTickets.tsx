import { ListChecksIcon } from 'lucide-react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { getMyTickets } from '@/api/tickets'
import { Card, CardContent } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useCurrentUser } from '@/features/auth/useCurrentUser'
import { TicketBadges } from '@/features/dashboard/TicketBadges'
import { SectionError, SectionSkeleton, DashboardSection } from '@/features/dashboard/DashboardSection'
import { StatCard } from '@/features/dashboard/StatCard'
import { TicketSlaTimers } from '@/features/sla/TicketSlaTimers'
import { useNow } from '@/features/sla/useNow'

const counterKeys = ['open', 'pending', 'breachedToday'] as const

/** My assigned tickets (not closed), most urgent SLA first as the server sorts them, with the counters (CRM-29). */
export function MyTickets() {
  const { t } = useTranslation()
  const now = useNow()
  const { data: user } = useCurrentUser()
  const mine = useQuery({ queryKey: ['tickets', 'mine'], queryFn: ({ signal }) => getMyTickets(signal) })
  const mineLink = user ? `/tickets?assignee=${encodeURIComponent(user.id)}` : '/tickets'
  const counterLinks = { open: mineLink, pending: `${mineLink}&status=pending`, breachedToday: mineLink }

  return (
    <DashboardSection
      id="my-tickets-title"
      level={3}
      title={t('dashboard.myTickets.title')}
      action={{ label: t('dashboard.myWork.viewAllMine'), to: mineLink }}
    >
      <div className="grid gap-4 sm:grid-cols-3">
        {counterKeys.map((key) => (
          <StatCard
            key={key}
            title={t(`dashboard.myTickets.counters.${key}`)}
            value={mine.data?.counters[key]}
            to={counterLinks[key]}
            icon={ListChecksIcon}
            accent={key === 'breachedToday' ? 'rose' : key === 'open' ? 'indigo' : 'amber'}
            tone={key === 'breachedToday' && (mine.data?.counters.breachedToday ?? 0) > 0 ? 'danger' : 'default'}
          />
        ))}
      </div>

      {mine.isPending ? <SectionSkeleton label={t('dashboard.myTickets.loading')} /> : null}
      {mine.isError ? <SectionError message={t('dashboard.myWork.loadError')} /> : null}

      {mine.data ? (
        mine.data.tickets.items.length === 0 ? (
          <p className="text-muted-foreground">{t('dashboard.myTickets.empty')}</p>
        ) : (
          <Card>
            <CardContent className="px-0">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('tickets.columns.number')}</TableHead>
                    <TableHead>{t('tickets.columns.subject')}</TableHead>
                    <TableHead className="hidden md:table-cell">{t('tickets.columns.customer')}</TableHead>
                    <TableHead>{t('tickets.columns.status')}</TableHead>
                    <TableHead className="hidden sm:table-cell">{t('tickets.columns.sla')}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {mine.data.tickets.items.slice(0, 8).map((ticket) => (
                    <TableRow key={ticket.id}>
                      <TableCell dir="ltr" className="text-start font-medium">
                        <Link to={`/tickets/${ticket.id}`} className="text-primary underline-offset-4 hover:underline">
                          {ticket.number}
                        </Link>
                      </TableCell>
                      <TableCell className="max-w-64 truncate">{ticket.subject}</TableCell>
                      <TableCell className="hidden md:table-cell">{ticket.customerName}</TableCell>
                      <TableCell>
                        <TicketBadges status={ticket.status} priority={ticket.priority} />
                      </TableCell>
                      <TableCell className="hidden sm:table-cell">
                        <TicketSlaTimers times={ticket} now={now} />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        )
      ) : null}
    </DashboardSection>
  )
}

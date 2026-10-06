import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { getMyTickets } from '@/api/tickets'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { TicketSlaTimers } from '@/features/sla/TicketSlaTimers'
import { useNow } from '@/features/sla/useNow'

const counterKeys = ['open', 'pending', 'breachedToday'] as const

/** My assigned tickets (not closed), most urgent SLA first as the server sorts them, with the counters (CRM-29). */
export function MyTickets() {
  const { t } = useTranslation()
  const now = useNow()
  const mine = useQuery({ queryKey: ['tickets', 'mine'], queryFn: ({ signal }) => getMyTickets(signal) })

  return (
    <section className="flex flex-col gap-4" aria-labelledby="my-tickets-title">
      <h2 id="my-tickets-title" className="text-xl font-semibold">
        {t('dashboard.myTickets.title')}
      </h2>

      {mine.isPending ? <p className="text-muted-foreground">{t('dashboard.myTickets.loading')}</p> : null}

      {mine.data ? (
        <>
          <div className="grid gap-4 sm:grid-cols-3">
            {counterKeys.map((key) => (
              <Card key={key} role="group" aria-label={t(`dashboard.myTickets.counters.${key}`)}>
                <CardHeader>
                  <CardTitle className="text-sm font-medium text-muted-foreground">
                    {t(`dashboard.myTickets.counters.${key}`)}
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <p className="text-3xl font-semibold">{mine.data.counters[key]}</p>
                </CardContent>
              </Card>
            ))}
          </div>

          {mine.data.tickets.items.length === 0 ? (
            <p className="text-muted-foreground">{t('dashboard.myTickets.empty')}</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('tickets.columns.number')}</TableHead>
                  <TableHead>{t('tickets.columns.subject')}</TableHead>
                  <TableHead>{t('tickets.columns.customer')}</TableHead>
                  <TableHead>{t('tickets.columns.status')}</TableHead>
                  <TableHead>{t('tickets.columns.priority')}</TableHead>
                  <TableHead>{t('tickets.columns.sla')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {mine.data.tickets.items.map((ticket) => (
                  <TableRow key={ticket.id}>
                    <TableCell dir="ltr" className="text-start font-medium">
                      <Link to={`/tickets/${ticket.id}`} className="text-primary underline-offset-4 hover:underline">
                        {ticket.number}
                      </Link>
                    </TableCell>
                    <TableCell>{ticket.subject}</TableCell>
                    <TableCell>{ticket.customerName}</TableCell>
                    <TableCell>
                      <Badge variant="secondary">{t(`tickets.statuses.${ticket.status}`)}</Badge>
                    </TableCell>
                    <TableCell>
                      <Badge variant={ticket.priority === 'high' ? 'destructive' : 'outline'}>
                        {t(`tickets.priorities.${ticket.priority}`)}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      <TicketSlaTimers times={ticket} now={now} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </>
      ) : null}
    </section>
  )
}

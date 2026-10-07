import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import type { Ticket } from '@/api/tickets'
import { ToneBadge } from "@/components/ui/tone-badge"
import { priorityTones, statusTones } from "./ticket-tones"
import { TicketSlaTimers } from '@/features/sla/TicketSlaTimers'
import { useNow } from '@/features/sla/useNow'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

/** Tickets in the order the API returns them (newest first). */
export function TicketsTable({ tickets }: { tickets: Ticket[] }) {
  const { t, i18n } = useTranslation()
  const now = useNow()
  const formatTime = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' })

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('tickets.columns.number')}</TableHead>
          <TableHead>{t('tickets.columns.subject')}</TableHead>
          <TableHead>{t('tickets.columns.customer')}</TableHead>
          <TableHead>{t('tickets.columns.status')}</TableHead>
          <TableHead>{t('tickets.columns.priority')}</TableHead>
          <TableHead>{t('tickets.columns.category')}</TableHead>
          <TableHead>{t('tickets.columns.assignee')}</TableHead>
          <TableHead>{t('tickets.columns.sla')}</TableHead>
          <TableHead>{t('tickets.columns.created')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {tickets.map((ticket) => (
          <TableRow key={ticket.id}>
            {/* Ticket numbers read left to right in Arabic too. */}
            <TableCell dir="ltr" className="text-start font-medium">
              <Link to={`/tickets/${ticket.id}`} className="text-primary underline-offset-4 hover:underline">
                {ticket.number}
              </Link>
            </TableCell>
            <TableCell>{ticket.subject}</TableCell>
            <TableCell>{ticket.customerName}</TableCell>
            <TableCell>
              <ToneBadge tone={statusTones[ticket.status]}>{t(`tickets.statuses.${ticket.status}`)}</ToneBadge>
            </TableCell>
            <TableCell>
              <ToneBadge tone={priorityTones[ticket.priority]}>
                {t(`tickets.priorities.${ticket.priority}`)}
              </ToneBadge>
            </TableCell>
            <TableCell>{ticket.categoryName}</TableCell>
            <TableCell className={ticket.assigneeName ? undefined : 'text-muted-foreground'}>
              {ticket.assigneeName ?? t('tickets.filters.unassigned')}
            </TableCell>
            <TableCell>
              <TicketSlaTimers times={ticket} now={now} />
            </TableCell>
            <TableCell>
              <time dateTime={ticket.createdAt}>{formatTime.format(new Date(ticket.createdAt))}</time>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}

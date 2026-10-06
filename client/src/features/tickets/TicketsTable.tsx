import { useTranslation } from 'react-i18next'
import type { Ticket } from '@/api/tickets'
import { Badge } from '@/components/ui/badge'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

/** Tickets in the order the API returns them (newest first). */
export function TicketsTable({ tickets }: { tickets: Ticket[] }) {
  const { t, i18n } = useTranslation()
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
          <TableHead>{t('tickets.columns.created')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {tickets.map((ticket) => (
          <TableRow key={ticket.id}>
            {/* Ticket numbers read left to right in Arabic too. */}
            <TableCell dir="ltr" className="text-start font-medium">
              {ticket.number}
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
            <TableCell>{ticket.categoryName}</TableCell>
            <TableCell className={ticket.assigneeName ? undefined : 'text-muted-foreground'}>
              {ticket.assigneeName ?? t('tickets.filters.unassigned')}
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

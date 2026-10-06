import { ArrowLeftIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { isApiError } from '@/api/errors'
import type { Ticket } from '@/api/tickets'
import { permissions } from '@/auth/permissions'
import { Badge } from '@/components/ui/badge'
import { Can } from '@/features/auth/Can'
import { TicketAssignControl } from '@/features/tickets/TicketAssignControl'
import { TicketReplyForm } from '@/features/tickets/TicketReplyForm'
import { TicketThread } from '@/features/tickets/TicketThread'
import { useTicket } from '@/features/tickets/useTickets'

/** One ticket: details, the conversation and the reply box (route tickets/:id). */
export function TicketDetailsPage() {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const ticket = useTicket(id)

  return (
    <div className="flex flex-col gap-6">
      <Link
        to="/tickets"
        className="flex w-fit items-center gap-1 text-sm text-primary underline-offset-4 hover:underline"
      >
        <ArrowLeftIcon aria-hidden="true" className="size-4 rtl:rotate-180" />
        {t('tickets.details.back')}
      </Link>

      {ticket.isPending ? (
        <p className="text-muted-foreground">{t('tickets.details.loading')}</p>
      ) : ticket.data ? (
        <TicketDetails ticket={ticket.data} />
      ) : (
        <p className="text-muted-foreground">
          {isApiError(ticket.error) && ticket.error.status === 404 ? t('tickets.details.notFound') : t('errors.generic')}
        </p>
      )}
    </div>
  )
}

function TicketDetails({ ticket }: { ticket: Ticket }) {
  const { t, i18n } = useTranslation()
  const formatTime = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' })

  return (
    <>
      <header className="flex flex-col gap-2">
        <p dir="ltr" className="text-sm font-medium text-muted-foreground">
          {ticket.number}
        </p>
        <h1 className="text-2xl font-semibold">{ticket.subject}</h1>
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant="secondary">{t(`tickets.statuses.${ticket.status}`)}</Badge>
          <Badge variant={ticket.priority === 'high' ? 'destructive' : 'outline'}>
            {t(`tickets.priorities.${ticket.priority}`)}
          </Badge>
        </div>
      </header>

      <dl className="grid gap-x-6 gap-y-3 text-sm sm:grid-cols-2 lg:grid-cols-3">
        <Detail label={t('tickets.columns.customer')}>
          <Link to={`/customers/${ticket.customerId}`} className="text-primary underline-offset-4 hover:underline">
            {ticket.customerName}
          </Link>
        </Detail>
        <Detail label={t('tickets.columns.category')}>{ticket.categoryName ?? t('tickets.noCategory')}</Detail>
        <Detail label={t('tickets.columns.assignee')}>{ticket.assigneeName ?? t('tickets.filters.unassigned')}</Detail>
        <Detail label={t('tickets.details.channel')}>{t(`tickets.channels.${ticket.channel}`)}</Detail>
        <Detail label={t('tickets.columns.created')}>
          <time dateTime={ticket.createdAt}>{formatTime.format(new Date(ticket.createdAt))}</time>
        </Detail>
        <Detail label={t('tickets.details.firstResponse')}>
          {ticket.firstResponseAt ? (
            <time dateTime={ticket.firstResponseAt}>{formatTime.format(new Date(ticket.firstResponseAt))}</time>
          ) : (
            t('tickets.details.noResponseYet')
          )}
        </Detail>
      </dl>

      <TicketAssignControl ticket={ticket} />

      {ticket.description ? (
        <p dir="auto" className="whitespace-pre-line wrap-break-word rounded-lg border p-3">
          {ticket.description}
        </p>
      ) : null}

      <TicketThread ticketId={ticket.id} customerName={ticket.customerName} />

      <Can permission={permissions.ticketsManage}>
        {ticket.status === 'closed' ? (
          <p className="rounded-lg border p-3 text-muted-foreground">{t('tickets.details.closedNotice')}</p>
        ) : (
          <TicketReplyForm ticketId={ticket.id} />
        )}
      </Can>
    </>
  )
}

function Detail({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5">
      <dt className="text-muted-foreground">{label}</dt>
      <dd>{children}</dd>
    </div>
  )
}

import { ArrowLeftIcon } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { isApiError } from '@/api/errors'
import type { Ticket } from '@/api/tickets'
import { permissions } from '@/auth/permissions'
import { Badge } from '@/components/ui/badge'
import { Can } from '@/features/auth/Can'
import { TicketAiClassification } from '@/features/ai/TicketAiClassification'
import { TicketSummary } from '@/features/ai/TicketSummary'
import { LinkedArticles } from '@/features/knowledge-base/LinkedArticles'
import { CustomerPanel } from '@/features/tickets/CustomerPanel'
import { TicketAssignControl } from '@/features/tickets/TicketAssignControl'
import { TicketClassifyControl } from '@/features/tickets/TicketClassifyControl'
import { TicketHistory } from '@/features/tickets/TicketHistory'
import { TicketReplyForm } from '@/features/tickets/TicketReplyForm'
import { TicketStatusActions } from '@/features/tickets/TicketStatusActions'
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
  const [tab, setTab] = useState<'conversation' | 'history'>('conversation')

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]">
      <div className="flex min-w-0 flex-col gap-6">
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
        {ticket.resolvedAt ? (
          <Detail label={t('tickets.details.resolvedAt')}>
            <time dateTime={ticket.resolvedAt}>{formatTime.format(new Date(ticket.resolvedAt))}</time>
          </Detail>
        ) : null}
        <Detail label={t('tickets.details.firstResponse')}>
          {ticket.firstResponseAt ? (
            <time dateTime={ticket.firstResponseAt}>{formatTime.format(new Date(ticket.firstResponseAt))}</time>
          ) : (
            t('tickets.details.noResponseYet')
          )}
        </Detail>
      </dl>

      <TicketStatusActions ticket={ticket} />
      <TicketAssignControl ticket={ticket} />
      <TicketClassifyControl ticket={ticket} />
      <TicketAiClassification ticketId={ticket.id} />
      <TicketSummary ticketId={ticket.id} />

      {ticket.description ? (
        <p dir="auto" className="whitespace-pre-line wrap-break-word rounded-lg border p-3">
          {ticket.description}
        </p>
      ) : null}

      <div role="tablist" aria-label={t('tickets.details.tabs')} className="flex gap-2 border-b">
        {(['conversation', 'history'] as const).map((name) => (
          <button
            key={name}
            type="button"
            role="tab"
            id={`ticket-tab-${name}`}
            aria-selected={tab === name}
            aria-controls="ticket-tabpanel"
            onClick={() => setTab(name)}
            className={`border-b-2 px-3 py-2 text-sm font-medium ${tab === name ? 'border-primary text-foreground' : 'border-transparent text-muted-foreground'}`}
          >
            {t(name === 'conversation' ? 'tickets.details.conversationTab' : 'tickets.details.historyTab')}
          </button>
        ))}
      </div>

      <div role="tabpanel" id="ticket-tabpanel" aria-labelledby={`ticket-tab-${tab}`}className="flex flex-col gap-6">
        {tab === 'history' ? (
          <TicketHistory ticketId={ticket.id} />
        ) : (
          <>
          <TicketThread ticketId={ticket.id} customerName={ticket.customerName} />
          <LinkedArticles ticketId={ticket.id} />

          <Can permission={permissions.ticketsManage}>
            {ticket.status === 'closed' ? (
              <p className="rounded-lg border p-3 text-muted-foreground">{t('tickets.details.closedNotice')}</p>
            ) : (
              <TicketReplyForm ticketId={ticket.id} />
            )}
          </Can>
          </>
        )}
      </div>
      </div>
      <Can permission={permissions.customersView}>
        <CustomerPanel ticketId={ticket.id} customerId={ticket.customerId} />
      </Can>
    </div>
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

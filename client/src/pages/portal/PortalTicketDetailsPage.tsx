import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { toast } from 'sonner'
import { isApiError } from '@/api/errors'
import {
  getPortalTicket,
  listPortalHistory,
  listPortalMessages,
  reopenPortalTicket,
  replyToPortalTicket,
  type PortalTicketSummary,
} from '@/api/portal'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Field, FieldError, FieldLabel } from '@/components/ui/field'
import { Textarea } from '@/components/ui/textarea'
import { TicketFeedback } from '@/features/portal/TicketFeedback'

/** One of the customer's requests: status, public conversation, history, reply box and reopen button. */
export function PortalTicketDetailsPage() {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const ticket = useQuery({ queryKey: ['portal', 'ticket', id], queryFn: ({ signal }) => getPortalTicket(id, signal), retry: false })

  return (
    <div className="flex flex-col gap-6">
      <Link to="/portal/tickets" className="w-fit text-sm text-primary underline-offset-4 hover:underline">
        {t('portal.details.back')}
      </Link>
      {ticket.isPending ? (
        <p className="text-muted-foreground">{t('portal.loading')}</p>
      ) : ticket.data ? (
        <Details ticket={ticket.data} />
      ) : (
        <p className="text-muted-foreground">
          {isApiError(ticket.error) && ticket.error.status === 404 ? t('portal.details.notFound') : t('errors.generic')}
        </p>
      )}
    </div>
  )
}

function Details({ ticket }: { ticket: PortalTicketSummary }) {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const [text, setText] = useState('')
  const [error, setError] = useState<string | null>(null)
  const formatTime = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' })
  const messages = useQuery({ queryKey: ['portal', 'messages', ticket.id], queryFn: ({ signal }) => listPortalMessages(ticket.id, signal) })
  const history = useQuery({ queryKey: ['portal', 'history', ticket.id], queryFn: ({ signal }) => listPortalHistory(ticket.id, signal) })
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['portal'] })

  const reply = useMutation({
    mutationFn: (body: string) => replyToPortalTicket(ticket.id, body),
    onSuccess: async () => {
      setText('')
      setError(null)
      toast.success(t('portal.details.replySent'))
      await refresh()
    },
  })
  const reopen = useMutation({
    mutationFn: () => reopenPortalTicket(ticket.id),
    onSuccess: async () => {
      toast.success(t('portal.details.reopened'))
      await refresh()
    },
  })

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    const body = text.trim()
    if (!body) {
      setError(t('portal.details.messageRequired'))
      return
    }
    try {
      await reply.mutateAsync(body)
    } catch (caught) {
      const errors = isApiError(caught) ? caught.problem?.errors : undefined
      const message = errors?.body?.[0] ?? errors?.status?.[0]
      if (message) setError(message)
    }
  }

  return (
    <>
      <header className="flex flex-col gap-2">
        <p dir="ltr" className="text-sm font-medium text-muted-foreground">
          {ticket.number}
        </p>
        <h1 className="text-2xl font-semibold text-primary">{ticket.subject}</h1>
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant="secondary">{t(`portal.statuses.${ticket.status}`)}</Badge>
          {ticket.categoryName ? <span className="text-sm text-muted-foreground">{ticket.categoryName}</span> : null}
        </div>
      </header>

      {ticket.description ? (
        <p dir="auto" className="whitespace-pre-line wrap-break-word rounded-lg border p-3">
          {ticket.description}
        </p>
      ) : null}

      {ticket.canReopen ? (
        <div className="flex flex-col gap-2 rounded-lg border p-3">
          <p>{t('portal.details.resolvedNotice')}</p>
          <div>
            <Button variant="outline" disabled={reopen.isPending} onClick={() => reopen.mutate()}>
              {t('portal.details.reopen')}
            </Button>
          </div>
        </div>
      ) : null}

      {ticket.status === 'resolved' || ticket.status === 'closed' ? <TicketFeedback ticketId={ticket.id} status={ticket.status} /> : null}

      <section aria-label={t('portal.details.conversation')} className="flex flex-col gap-3">
        <h2 className="text-lg font-semibold text-primary">{t('portal.details.conversation')}</h2>
        {messages.data && messages.data.length > 0 ? (
          <ul className="flex flex-col gap-2">
            {messages.data.map((message) => (
              <li key={message.id} className={`flex flex-col gap-1 rounded-lg border p-3 ${message.fromCustomer ? 'bg-muted' : ''}`}>
                <span className="text-sm text-muted-foreground">
                  {message.fromCustomer ? t('portal.details.you') : (message.authorName ?? t('portal.details.support'))} ·{' '}
                  <time dateTime={message.createdAt}>{formatTime.format(new Date(message.createdAt))}</time>
                </span>
                <p dir="auto" className="whitespace-pre-line wrap-break-word">
                  {message.body}
                </p>
              </li>
            ))}
          </ul>
        ) : (
          <p className="text-muted-foreground">{t('portal.details.noMessages')}</p>
        )}
      </section>

      {ticket.canReply ? (
        <form noValidate onSubmit={onSubmit} aria-label={t('portal.details.replyForm')} className="flex flex-col gap-3">
          <Field data-invalid={error !== null}>
            <FieldLabel htmlFor="portal-reply">{t('portal.details.reply')}</FieldLabel>
            <Textarea id="portal-reply" dir="auto" rows={4} value={text} aria-invalid={error !== null} onChange={(event) => setText(event.target.value)} />
            {error ? <FieldError errors={[{ message: error }]} /> : null}
          </Field>
          <div>
            <Button type="submit" disabled={reply.isPending}>
              {t('portal.details.send')}
            </Button>
          </div>
        </form>
      ) : null}

      <section aria-label={t('portal.details.history')} className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-primary">{t('portal.details.history')}</h2>
        <ul className="flex flex-col gap-1 text-sm">
          {history.data?.map((item, index) => (
            <li key={`${item.type}-${index}`}>
              <time dateTime={item.at}>{formatTime.format(new Date(item.at))}</time>
              {' · '}
              {item.type === 'created'
                ? t('portal.details.historyCreated')
                : t('portal.details.historyStatus', { status: t(`portal.statuses.${(item.status ?? 'open') as 'open'}`) })}
            </li>
          ))}
        </ul>
      </section>
    </>
  )
}

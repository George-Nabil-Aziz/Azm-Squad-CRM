import { useTranslation } from 'react-i18next'
import type { TicketMessage } from '@/api/tickets'
import { Badge } from '@/components/ui/badge'
import { useTicketMessages } from './useTickets'

interface TicketThreadProps {
  ticketId: string
  /** Shown as the author of customer (inbound) messages. */
  customerName: string
}

/** The conversation of a ticket, oldest first, with author and time; internal notes are flagged. */
export function TicketThread({ ticketId, customerName }: TicketThreadProps) {
  const { t, i18n } = useTranslation()
  const messages = useTicketMessages(ticketId)
  const formatTime = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' })

  return (
    <section className="flex flex-col gap-3">
      <h2 className="text-lg font-semibold text-primary">{t('tickets.details.conversation')}</h2>
      {messages.isPending ? (
        <p className="text-muted-foreground">{t('tickets.details.loadingMessages')}</p>
      ) : messages.data && messages.data.length > 0 ? (
        <ul aria-label={t('tickets.details.conversation')} className="flex flex-col gap-3">
          {messages.data.map((message) => (
            <MessageItem
              key={message.id}
              message={message}
              author={message.authorName ?? (message.direction === 'inbound' ? customerName : t('tickets.details.system'))}
              time={formatTime.format(new Date(message.createdAt))}
            />
          ))}
        </ul>
      ) : (
        <p className="text-muted-foreground">{t('tickets.details.noMessages')}</p>
      )}
    </section>
  )
}

function MessageItem({ message, author, time }: { message: TicketMessage; author: string; time: string }) {
  const { t } = useTranslation()
  const tone = message.isInternal
    ? 'border-dashed bg-muted'
    : message.direction === 'outbound'
      ? 'bg-accent'
      : 'bg-card'

  return (
    <li className={`flex flex-col gap-1 rounded-lg border p-3 ${tone}`}>
      <p className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
        <span className="font-medium text-foreground">{author}</span>
        {message.isInternal ? <Badge variant="outline">{t('tickets.details.internalNote')}</Badge> : null}
        <span>
          {'· '}
          <time dateTime={message.createdAt}>{time}</time>
        </span>
        {message.deliveryStatus ? <span>{t(`tickets.details.delivery.${message.deliveryStatus}`)}</span> : null}
      </p>
      <p dir="auto" className="whitespace-pre-line wrap-break-word">
        {message.body}
      </p>
    </li>
  )
}

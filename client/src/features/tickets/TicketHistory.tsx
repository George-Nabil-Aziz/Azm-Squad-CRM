import { useTranslation } from 'react-i18next'
import type { TicketHistoryItem } from '@/api/tickets'
import { ticketPriorities, ticketStatuses } from './ticket-values'
import { useTicketHistory } from './useTicketHistory'

/** The audit trail of a ticket, oldest first: what changed, from what to what, by whom and when. Read-only. */
export function TicketHistory({ ticketId }: { ticketId: string }) {
  const { t, i18n } = useTranslation()
  const history = useTicketHistory(ticketId)
  const formatTime = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' })

  /** Status / priority values are codes with a translation; assignee / category values are names; null means nothing. */
  function valueText(item: TicketHistoryItem, value: string | null) {
    if (value === null) return t('tickets.history.none')
    if (item.field === 'status' && (ticketStatuses as readonly string[]).includes(value)) {
      return t(`tickets.statuses.${value as (typeof ticketStatuses)[number]}`)
    }
    if (item.field === 'priority' && (ticketPriorities as readonly string[]).includes(value)) {
      return t(`tickets.priorities.${value as (typeof ticketPriorities)[number]}`)
    }
    return value
  }

  if (history.isPending) return <p className="text-muted-foreground">{t('tickets.history.loading')}</p>
  if (!history.data || history.data.length === 0) {
    return <p className="text-muted-foreground">{t('tickets.history.empty')}</p>
  }

  return (
    <ul aria-label={t('tickets.history.title')} className="flex flex-col gap-3">
      {history.data.map((item) => (
        <li key={item.id} className="flex flex-col gap-1 rounded-lg border p-3">
          <p className="font-medium">
            {item.field === 'escalation'
              ? t('tickets.history.escalated', { level: item.newValue })
              : `${t(`tickets.history.fields.${item.field}`)}: ${valueText(item, item.oldValue)} → ${valueText(item, item.newValue)}`}
          </p>
          <p className="text-sm text-muted-foreground">
            {item.changedByName ?? t('tickets.history.system')}
            {' · '}
            <time dateTime={item.changedAt}>{formatTime.format(new Date(item.changedAt))}</time>
          </p>
        </li>
      ))}
    </ul>
  )
}

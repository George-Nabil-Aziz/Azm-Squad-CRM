import { useTranslation } from 'react-i18next'
import { Badge } from '@/components/ui/badge'
import type { TicketPriority, TicketStatus } from '@/features/tickets/ticket-values'

/** Status and priority of a ticket as badges (high priority in the destructive colour). */
export function TicketBadges({ status, priority }: { status?: TicketStatus; priority: TicketPriority }) {
  const { t } = useTranslation()
  return (
    <div className="flex flex-wrap gap-1">
      {status ? <Badge variant="secondary">{t(`tickets.statuses.${status}`)}</Badge> : null}
      <Badge variant={priority === 'high' ? 'destructive' : 'outline'}>{t(`tickets.priorities.${priority}`)}</Badge>
    </div>
  )
}

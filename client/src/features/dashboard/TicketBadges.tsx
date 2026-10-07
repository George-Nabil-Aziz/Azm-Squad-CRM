import { useTranslation } from 'react-i18next'
import { ToneBadge } from '@/components/ui/tone-badge'
import { priorityTones, statusTones } from '@/features/tickets/ticket-tones'
import type { TicketPriority, TicketStatus } from '@/features/tickets/ticket-values'

/** Status and priority of a ticket as soft coloured badges (high priority in rose). */
export function TicketBadges({ status, priority }: { status?: TicketStatus; priority: TicketPriority }) {
  const { t } = useTranslation()
  return (
    <div className="flex flex-wrap gap-1">
      {status ? <ToneBadge tone={statusTones[status]}>{t(`tickets.statuses.${status}`)}</ToneBadge> : null}
      <ToneBadge tone={priorityTones[priority]}>{t(`tickets.priorities.${priority}`)}</ToneBadge>
    </div>
  )
}

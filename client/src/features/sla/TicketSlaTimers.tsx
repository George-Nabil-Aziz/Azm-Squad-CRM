import type { TFunction } from 'i18next'
import { useTranslation } from 'react-i18next'
import { Badge } from '@/components/ui/badge'
import { formatMinutes } from './sla-format'
import { slaTimer, type SlaTimerState } from './sla-timer'

/** The SLA times of a ticket (server: TicketResponse), UTC ISO strings or null. */
export interface TicketSlaTimes {
  responseDueAt: string | null
  firstResponseAt: string | null
  resolutionDueAt: string | null
  resolvedAt: string | null
}

const variants = {
  remaining: 'secondary',
  overdue: 'destructive',
  met: 'outline',
  metLate: 'outline',
} as const

function describe(timer: SlaTimerState, t: TFunction): string {
  switch (timer.state) {
    case 'remaining':
      return t('sla.timer.left', { time: formatMinutes(timer.minutes, t) })
    case 'overdue':
      return t('sla.timer.overdue', { time: formatMinutes(timer.minutes, t) })
    case 'met':
      return t('sla.timer.met')
    case 'metLate':
      return t('sla.timer.metLate')
    default:
      return t('sla.timer.none')
  }
}

/** Response and resolution countdowns of a ticket at `now` (CRM-20 AC 3). */
export function TicketSlaTimers({ times, now }: { times: TicketSlaTimes; now: Date }) {
  const { t } = useTranslation()
  if (!times.responseDueAt && !times.resolutionDueAt) {
    return <span className="text-muted-foreground">{t('sla.timer.none')}</span>
  }

  const timers = [
    { key: 'response', timer: slaTimer(times.responseDueAt, times.firstResponseAt, now) },
    { key: 'resolution', timer: slaTimer(times.resolutionDueAt, times.resolvedAt, now) },
  ] as const

  return (
    <div className="flex flex-col items-start gap-1">
      {timers.map(({ key, timer }) => (
        <Badge key={key} variant={timer.state === 'none' ? 'outline' : variants[timer.state]}>
          {t(`sla.timer.${key}`, { status: describe(timer, t) })}
        </Badge>
      ))}
    </div>
  )
}

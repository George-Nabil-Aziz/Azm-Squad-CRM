import type { TFunction } from 'i18next'

/** A duration in whole minutes as "45 min", "2 h" or "1 h 30 min" (units translated). */
export function formatMinutes(totalMinutes: number, t: TFunction): string {
  const hours = Math.floor(totalMinutes / 60)
  const minutes = totalMinutes % 60
  if (hours === 0) return t('sla.duration.minutes', { minutes })
  if (minutes === 0) return t('sla.duration.hours', { hours })
  return t('sla.duration.hoursMinutes', { hours, minutes })
}

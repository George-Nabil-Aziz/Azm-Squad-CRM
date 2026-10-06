/** State of one SLA timer (response or resolution) at a moment. */
export type SlaTimerState =
  | { state: 'none' }
  | { state: 'remaining'; minutes: number }
  | { state: 'overdue'; minutes: number }
  | { state: 'met' }
  | { state: 'metLate' }

const MINUTE_MS = 60_000

/**
 * Where a ticket stands against one due time: no due time, whole minutes left, minutes overdue, or met (in time /
 * late) once the target time (first response / resolution) is set. Times are ISO strings from the API.
 */
export function slaTimer(dueAt: string | null, metAt: string | null, now: Date): SlaTimerState {
  if (!dueAt) return { state: 'none' }
  const due = Date.parse(dueAt)
  if (metAt) return Date.parse(metAt) <= due ? { state: 'met' } : { state: 'metLate' }
  const left = due - now.getTime()
  return left > 0
    ? { state: 'remaining', minutes: Math.floor(left / MINUTE_MS) }
    : { state: 'overdue', minutes: Math.floor((now.getTime() - due) / MINUTE_MS) }
}

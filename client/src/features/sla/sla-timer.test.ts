import { describe, expect, it } from 'vitest'
import { slaTimer } from './sla-timer'

const now = new Date('2026-10-01T10:00:00Z')

describe('slaTimer', () => {
  it('has nothing to show without a due time', () => {
    expect(slaTimer(null, null, now)).toEqual({ state: 'none' })
  })

  it('counts the whole minutes left until the due time', () => {
    expect(slaTimer('2026-10-01T12:15:30Z', null, now)).toEqual({ state: 'remaining', minutes: 135 })
  })

  it('counts the minutes past the due time when overdue', () => {
    expect(slaTimer('2026-10-01T09:20:00Z', null, now)).toEqual({ state: 'overdue', minutes: 40 })
  })

  it('is overdue from the due time on', () => {
    expect(slaTimer('2026-10-01T10:00:00Z', null, now)).toEqual({ state: 'overdue', minutes: 0 })
  })

  it('is met when the target was reached in time, late when after the due time', () => {
    expect(slaTimer('2026-10-01T12:00:00Z', '2026-10-01T11:00:00Z', now)).toEqual({ state: 'met' })
    expect(slaTimer('2026-10-01T09:00:00Z', '2026-10-01T09:30:00Z', now)).toEqual({ state: 'metLate' })
  })
})

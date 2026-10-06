import { describe, expect, it } from 'vitest'
import ar from '@/i18n/ar.json'
import en from '@/i18n/en.json'
import { ticketPriorities } from './ticket-values'

describe('ticket priorities', () => {
  it('are exactly High, Mid and Low (server: TicketValues.PriorityNames)', () => {
    expect(ticketPriorities).toEqual(['high', 'mid', 'low'])
  })

  it('have an English and an Arabic label', () => {
    expect(ticketPriorities.map((priority) => en.tickets.priorities[priority])).toEqual(['High', 'Mid', 'Low'])
    for (const priority of ticketPriorities) {
      expect(ar.tickets.priorities[priority]).toMatch(/[؀-ۿ]/)
    }
  })
})

import { describe, expect, it } from 'vitest'
import ar from '@/i18n/ar.json'
import en from '@/i18n/en.json'
import { ticketChannels, ticketPriorities, ticketStatuses } from './ticket-values'

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

describe('ticket statuses and channels', () => {
  it('match the server names (TicketValues)', () => {
    expect(ticketStatuses).toEqual(['new', 'open', 'pending', 'resolved', 'closed'])
    expect(ticketChannels).toEqual(['manual', 'email', 'whatsapp', 'portal', 'webform', 'chat', 'sms'])
  })

  it('have an English and an Arabic label', () => {
    expect(ticketStatuses.map((status) => en.tickets.statuses[status])).toEqual([
      'New',
      'Open',
      'Pending',
      'Resolved',
      'Closed',
    ])
    for (const status of ticketStatuses) expect(ar.tickets.statuses[status]).toMatch(/[؀-ۿ]/)
    for (const channel of ticketChannels) {
      expect(en.tickets.channels[channel]).not.toBe('')
      expect(ar.tickets.channels[channel]).toMatch(/[؀-ۿ]/)
    }
  })
})

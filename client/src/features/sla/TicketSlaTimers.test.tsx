import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { TicketSlaTimers } from './TicketSlaTimers'

const now = new Date('2026-10-01T10:00:00Z')

describe('TicketSlaTimers', () => {
  it('shows the time left for the response and how long the resolution is overdue', () => {
    render(
      <TicketSlaTimers
        times={{
          responseDueAt: '2026-10-01T11:00:00Z',
          firstResponseAt: null,
          resolutionDueAt: '2026-10-01T09:30:00Z',
          resolvedAt: null,
        }}
        now={now}
      />,
    )

    expect(screen.getByText('Response: 1 h left')).toBeInTheDocument()
    expect(screen.getByText('Resolution: overdue by 30 min')).toBeInTheDocument()
  })

  it('shows met targets', () => {
    render(
      <TicketSlaTimers
        times={{
          responseDueAt: '2026-10-01T11:00:00Z',
          firstResponseAt: '2026-10-01T09:10:00Z',
          resolutionDueAt: '2026-10-01T09:30:00Z',
          resolvedAt: '2026-10-01T09:45:00Z',
        }}
        now={now}
      />,
    )

    expect(screen.getByText('Response: met')).toBeInTheDocument()
    expect(screen.getByText('Resolution: met late')).toBeInTheDocument()
  })

  it('says when a ticket has no SLA', () => {
    render(
      <TicketSlaTimers
        times={{ responseDueAt: null, firstResponseAt: null, resolutionDueAt: null, resolvedAt: null }}
        now={now}
      />,
    )

    expect(screen.getByText('No SLA')).toBeInTheDocument()
  })

  it('shows the escalation level of an escalated ticket', () => {
    render(
      <TicketSlaTimers
        times={{
          responseDueAt: '2026-10-01T09:00:00Z',
          firstResponseAt: null,
          resolutionDueAt: '2026-10-01T18:00:00Z',
          resolvedAt: null,
          escalationLevel: 2,
        }}
        now={now}
      />,
    )

    expect(screen.getByText('Escalated (level 2)')).toBeInTheDocument()
  })

  it('shows no escalation badge at level 0', () => {
    render(
      <TicketSlaTimers
        times={{
          responseDueAt: '2026-10-01T09:00:00Z',
          firstResponseAt: null,
          resolutionDueAt: '2026-10-01T18:00:00Z',
          resolvedAt: null,
          escalationLevel: 0,
        }}
        now={now}
      />,
    )

    expect(screen.queryByText(/Escalated/)).not.toBeInTheDocument()
  })
})

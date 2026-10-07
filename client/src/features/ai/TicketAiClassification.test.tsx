import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getTicketClassification, type TicketClassification as Classification } from '@/api/ai'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { changeTicketCategory, changeTicketPriority } from '@/api/tickets'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { TicketAiClassification } from './TicketAiClassification'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))
vi.mock('@/api/ai', () => ({ getTicketClassification: vi.fn() }))
vi.mock('@/api/tickets', () => ({ changeTicketCategory: vi.fn(), changeTicketPriority: vi.fn() }))

const agent: CurrentUser = {
  id: '2',
  email: 'agent@crm.local',
  fullName: 'Sara Agent',
  roles: ['Agent'],
  permissions: [permissions.ticketsView, permissions.ticketsManage],
}

const base: Classification = {
  status: 'applied',
  suggestedCategoryId: 'k1',
  suggestedCategoryName: 'Billing',
  suggestedPriority: 'high',
  confidence: 0.92,
  categoryApplied: true,
  priorityApplied: true,
  createdAt: '2026-10-01T08:00:00Z',
  categoryOverriddenAt: null,
  priorityOverriddenAt: null,
}

function renderIt() {
  render(
    <QueryClientProvider client={createQueryClient()}>
      <TicketAiClassification ticketId="t1" />
    </QueryClientProvider>,
  )
}

describe('AI classification of a ticket', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(agent)
    vi.mocked(getTicketClassification).mockReset().mockResolvedValue(base)
    vi.mocked(changeTicketCategory).mockReset().mockResolvedValue({} as never)
    vi.mocked(changeTicketPriority).mockReset().mockResolvedValue({} as never)
  })

  it('shows what was applied automatically with the confidence', async () => {
    renderIt()

    const region = await screen.findByRole('region', { name: 'AI classification' })
    expect(region).toHaveTextContent('Billing')
    expect(region).toHaveTextContent('92%')
    expect(region).toHaveTextContent('Applied automatically')
    expect(screen.queryByRole('button', { name: 'Apply suggestion' })).not.toBeInTheDocument()
  })

  it('shows a low-confidence result as a suggestion that can be applied', async () => {
    vi.mocked(getTicketClassification).mockResolvedValue({ ...base, status: 'suggested', confidence: 0.5, categoryApplied: false, priorityApplied: false })
    renderIt()

    expect(await screen.findByText('Suggestion only')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Apply suggestion' }))

    await waitFor(() => expect(changeTicketCategory).toHaveBeenCalledWith('t1', 'k1'))
    expect(changeTicketPriority).toHaveBeenCalledWith('t1', 'high')
  })

  it('mentions an override by an agent', async () => {
    vi.mocked(getTicketClassification).mockResolvedValue({ ...base, categoryOverriddenAt: '2026-10-01T09:00:00Z' })
    renderIt()

    expect(await screen.findByText(/changed by an agent/)).toBeInTheDocument()
  })

  it('shows nothing when the ticket has no classification', async () => {
    vi.mocked(getTicketClassification).mockResolvedValue({ ...base, status: 'none', suggestedCategoryId: null, confidence: null })
    renderIt()

    await waitFor(() => expect(getTicketClassification).toHaveBeenCalled())
    expect(screen.queryByRole('region', { name: 'AI classification' })).not.toBeInTheDocument()
  })

  it('hides the apply button from someone who may not manage tickets', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue({ ...agent, permissions: [permissions.ticketsView] })
    vi.mocked(getTicketClassification).mockResolvedValue({ ...base, status: 'suggested', categoryApplied: false, priorityApplied: false })
    renderIt()

    await screen.findByText('Suggestion only')
    expect(screen.queryByRole('button', { name: 'Apply suggestion' })).not.toBeInTheDocument()
  })
})

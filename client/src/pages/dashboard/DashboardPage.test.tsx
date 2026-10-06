import { QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { getHealth } from '@/api/health'
import { getMyTickets, type MyTickets, type Ticket } from '@/api/tickets'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { DashboardPage } from './DashboardPage'

vi.mock('@/api/health', () => ({ getHealth: vi.fn() }))
vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))
vi.mock('@/api/tickets', () => ({ getMyTickets: vi.fn() }))

const admin: CurrentUser = {
  id: '1',
  email: 'admin@crm.local',
  fullName: 'System Administrator',
  roles: ['SuperAdmin'],
  permissions: [permissions.ticketsView],
}

function ticket(id: string, number: string, subject: string, priority: Ticket['priority'], status: Ticket['status']): Ticket {
  return {
    id,
    number,
    subject,
    description: null,
    status,
    priority,
    channel: 'manual',
    customerId: 'c1',
    customerName: 'Nour Trading',
    categoryId: null,
    categoryName: null,
    assigneeId: '1',
    assigneeName: 'System Administrator',
    createdAt: '2026-10-01T08:00:00Z',
    updatedAt: '2026-10-01T08:00:00Z',
    responseDueAt: '2026-10-01T10:00:00Z',
    resolutionDueAt: '2026-10-02T08:00:00Z',
    firstResponseAt: null,
    resolvedAt: null,
    allowedStatuses: [],
    responseBreached: false,
    resolutionBreached: false,
    escalationLevel: 0,
    responseWarnedAt: null,
  }
}

const mine: MyTickets = {
  counters: { open: 2, pending: 1, breachedToday: 1 },
  tickets: {
    items: [
      ticket('t2', 'TKT-000002', 'Printer is down', 'high', 'open'),
      ticket('t1', 'TKT-000001', 'Invoice is wrong', 'low', 'pending'),
    ],
    page: 1,
    pageSize: 50,
    totalCount: 2,
  },
}

function renderDashboard() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('DashboardPage', () => {
  beforeEach(() => {
    vi.mocked(getHealth).mockReset()
    vi.mocked(getCurrentUser).mockResolvedValue(admin)
    vi.mocked(getMyTickets).mockReset().mockResolvedValue(mine)
  })

  it('welcomes the signed-in user', async () => {
    vi.mocked(getHealth).mockResolvedValue({ status: 'ok' })
    renderDashboard()

    expect(screen.getByRole('heading', { level: 1, name: 'Dashboard' })).toBeInTheDocument()
    expect(await screen.findByText('Welcome, System Administrator')).toBeInTheDocument()
  })

  it('shows "ok" when the API is healthy', async () => {
    vi.mocked(getHealth).mockResolvedValue({ status: 'ok' })
    renderDashboard()

    expect(await screen.findByText('ok')).toBeInTheDocument()
  })

  it('shows "unavailable" when the API call fails', async () => {
    vi.mocked(getHealth).mockRejectedValue(new Error('503'))
    renderDashboard()

    expect(await screen.findByText('unavailable')).toBeInTheDocument()
  })

  it('shows the counters: open, pending and breached today', async () => {
    vi.mocked(getHealth).mockResolvedValue({ status: 'ok' })
    renderDashboard()

    const open = await screen.findByRole('group', { name: 'Open' })
    expect(within(open).getByText('2')).toBeInTheDocument()
    expect(within(screen.getByRole('group', { name: 'Pending' })).getByText('1')).toBeInTheDocument()
    expect(within(screen.getByRole('group', { name: 'Breached today' })).getByText('1')).toBeInTheDocument()
  })

  it('lists my tickets in the order of the server, each linking to its details', async () => {
    vi.mocked(getHealth).mockResolvedValue({ status: 'ok' })
    renderDashboard()

    await screen.findByRole('link', { name: 'TKT-000002' })

    const rows = screen.getAllByRole('row').slice(1)
    expect(rows).toHaveLength(2)
    expect(within(rows[0]).getByRole('link', { name: 'TKT-000002' })).toHaveAttribute('href', '/tickets/t2')
    expect(within(rows[0]).getByText('Printer is down')).toBeInTheDocument()
    expect(within(rows[1]).getByRole('link', { name: 'TKT-000001' })).toHaveAttribute('href', '/tickets/t1')
  })

  it('shows a message when no ticket is assigned to me', async () => {
    vi.mocked(getHealth).mockResolvedValue({ status: 'ok' })
    vi.mocked(getMyTickets).mockResolvedValue({
      counters: { open: 0, pending: 0, breachedToday: 0 },
      tickets: { items: [], page: 1, pageSize: 50, totalCount: 0 },
    })
    renderDashboard()

    expect(await screen.findByText('No tickets are assigned to you.')).toBeInTheDocument()
  })

  it('does not ask for my tickets without tickets.view', async () => {
    vi.mocked(getHealth).mockResolvedValue({ status: 'ok' })
    vi.mocked(getCurrentUser).mockResolvedValue({ ...admin, permissions: [] })
    renderDashboard()
    await screen.findByText('Welcome, System Administrator')

    expect(getMyTickets).not.toHaveBeenCalled()
    expect(screen.queryByRole('heading', { name: 'My tickets' })).not.toBeInTheDocument()
  })
})

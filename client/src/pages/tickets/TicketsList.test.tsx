import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import type { PagedResult } from '@/api/paging'
import { listTicketCategories, type TicketCategory } from '@/api/ticket-categories'
import { listTicketAssignees, listTickets, type Ticket } from '@/api/tickets'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { TicketsPage } from './TicketsPage'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))
vi.mock('@/api/customers', () => ({ listCustomers: vi.fn() }))
vi.mock('@/api/ticket-categories', () => ({ listTicketCategories: vi.fn() }))
vi.mock('@/api/tickets', () => ({ createTicket: vi.fn(), listTickets: vi.fn(), listTicketAssignees: vi.fn() }))

const signedInAgent: CurrentUser = {
  id: '2',
  email: 'agent@crm.local',
  fullName: 'Sara Agent',
  roles: ['Agent'],
  permissions: [permissions.customersView, permissions.customersManage, permissions.ticketsView, permissions.ticketsManage],
}

function ticket(overrides: Partial<Ticket>): Ticket {
  return {
    id: 't1',
    number: 'TKT-000001',
    subject: 'Invoice is wrong',
    description: null,
    status: 'new',
    priority: 'mid',
    channel: 'manual',
    customerId: 'c1',
    customerName: 'Nour Trading',
    categoryId: null,
    categoryName: null,
    assigneeId: null,
    assigneeName: null,
    createdAt: '2026-10-01T08:00:00Z',
    updatedAt: '2026-10-01T08:00:00Z',
    firstResponseAt: null,
    resolvedAt: null,
    allowedStatuses: [],
    ...overrides,
  }
}

const newer = ticket({
  id: 't2',
  number: 'TKT-000002',
  subject: 'Printer offline',
  status: 'open',
  priority: 'high',
  categoryId: 'k1',
  categoryName: 'Billing',
  assigneeId: 'u1',
  assigneeName: 'Omar Lead',
  createdAt: '2026-10-02T08:00:00Z',
})
const older = ticket({})

const billing: TicketCategory = {
  id: 'k1',
  name: 'Billing',
  isActive: true,
  createdAt: '2026-10-01T08:00:00Z',
  updatedAt: '2026-10-01T08:00:00Z',
}
const legacy: TicketCategory = { ...billing, id: 'k2', name: 'Legacy', isActive: false }

function pageOf(items: Ticket[], totalCount = items.length, page = 1): PagedResult<Ticket> {
  return { items, page, pageSize: 20, totalCount }
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter>
        <TicketsPage />
      </MemoryRouter>
      <ApiErrorToaster />
    </QueryClientProvider>,
  )
}

function lastListCall() {
  return vi.mocked(listTickets).mock.lastCall?.[0]
}

describe('TicketsPage — list and filters', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(signedInAgent)
    vi.mocked(listTickets).mockReset().mockResolvedValue(pageOf([newer, older]))
    vi.mocked(listTicketAssignees).mockReset().mockResolvedValue([{ id: 'u1', fullName: 'Omar Lead' }])
    vi.mocked(listTicketCategories).mockReset().mockResolvedValue([billing, legacy])
  })

  it('links each ticket number to its details page', async () => {
    renderPage()

    const row = await screen.findByRole('row', { name: /TKT-000002/ })
    expect(within(row).getByRole('link', { name: 'TKT-000002' })).toHaveAttribute('href', '/tickets/t2')
  })

  it('lists tickets newest first (as the API returns them) with translated status and priority', async () => {
    renderPage()

    const row = await screen.findByRole('row', { name: /TKT-000002/ })
    expect(within(row).getByText('Printer offline')).toBeInTheDocument()
    expect(within(row).getByText('Nour Trading')).toBeInTheDocument()
    expect(within(row).getByText('Open')).toBeInTheDocument()
    expect(within(row).getByText('High')).toBeInTheDocument()
    expect(within(row).getByText('Billing')).toBeInTheDocument()
    expect(within(row).getByText('Omar Lead')).toBeInTheDocument()
    const rows = screen.getAllByRole('row').slice(1)
    expect(rows.map((r) => within(r).getAllByRole('cell')[0].textContent)).toEqual(['TKT-000002', 'TKT-000001'])
    expect(within(screen.getByRole('row', { name: /TKT-000001/ })).getByText('Unassigned')).toBeInTheDocument()
    expect(listTickets).toHaveBeenCalledWith({ page: 1, pageSize: 20 }, expect.anything())
  })

  it('says "No tickets found." when nothing matches', async () => {
    vi.mocked(listTickets).mockResolvedValue(pageOf([]))
    renderPage()

    expect(await screen.findByText('No tickets found.')).toBeInTheDocument()
  })

  it('filters by status, priority, category, assignee and date range, starting again at page 1', async () => {
    vi.mocked(listTickets).mockResolvedValue(pageOf([newer, older], 45))
    renderPage()
    await screen.findByRole('row', { name: /TKT-000002/ })
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() => expect(lastListCall()).toEqual({ page: 2, pageSize: 20 }))

    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'open' } })
    await waitFor(() => expect(lastListCall()).toEqual({ status: 'open', page: 1, pageSize: 20 }))

    fireEvent.change(screen.getByLabelText('Priority'), { target: { value: 'high' } })
    const category = screen.getByLabelText('Category')
    await waitFor(() => expect(within(category).getAllByRole('option').map((o) => o.textContent)).toEqual(['All', 'Billing', 'Legacy']))
    fireEvent.change(category, { target: { value: 'k2' } })
    const assignee = screen.getByLabelText('Assignee')
    await waitFor(() => expect(within(assignee).getByRole('option', { name: 'Omar Lead' })).toBeInTheDocument())
    fireEvent.change(assignee, { target: { value: 'u1' } })
    fireEvent.change(screen.getByLabelText('Created from'), { target: { value: '2026-10-01' } })
    fireEvent.change(screen.getByLabelText('Created to'), { target: { value: '2026-10-05' } })

    // Combined filters: every one is sent (the API returns only tickets matching all of them).
    await waitFor(() =>
      expect(lastListCall()).toEqual({
        status: 'open',
        priority: 'high',
        categoryId: 'k2',
        assigneeId: 'u1',
        createdFrom: '2026-10-01',
        createdTo: '2026-10-05',
        page: 1,
        pageSize: 20,
      }),
    )
    expect(listTicketCategories).toHaveBeenCalledWith({}, expect.anything())
  })

  it('filters unassigned tickets', async () => {
    renderPage()
    await screen.findByRole('row', { name: /TKT-000002/ })

    fireEvent.change(screen.getByLabelText('Assignee'), { target: { value: 'unassigned' } })

    await waitFor(() => expect(lastListCall()).toEqual({ unassigned: true, page: 1, pageSize: 20 }))
  })

  it('searches by ticket number or subject on submit', async () => {
    renderPage()
    await screen.findByRole('row', { name: /TKT-000002/ })

    fireEvent.change(screen.getByRole('searchbox', { name: 'Search by ticket number or subject' }), {
      target: { value: ' TKT-000002 ' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Search' }))

    await waitFor(() => expect(lastListCall()).toEqual({ search: 'TKT-000002', page: 1, pageSize: 20 }))
  })

  it('clears every filter and the search', async () => {
    renderPage()
    await screen.findByRole('row', { name: /TKT-000002/ })
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'pending' } })
    fireEvent.change(screen.getByRole('searchbox', { name: 'Search by ticket number or subject' }), {
      target: { value: 'printer' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Search' }))
    await waitFor(() => expect(lastListCall()).toEqual({ status: 'pending', search: 'printer', page: 1, pageSize: 20 }))

    fireEvent.click(screen.getByRole('button', { name: 'Clear filters' }))

    expect(screen.getByLabelText('Status')).toHaveValue('')
    expect(screen.getByRole('searchbox', { name: 'Search by ticket number or subject' })).toHaveValue('')
    // The unfiltered first page is shown again (from the cache); the next filter is the only one sent.
    expect(await screen.findByRole('row', { name: /TKT-000002/ })).toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('Priority'), { target: { value: 'low' } })
    await waitFor(() => expect(lastListCall()).toEqual({ priority: 'low', page: 1, pageSize: 20 }))
  })

  it('pages through the results', async () => {
    vi.mocked(listTickets).mockResolvedValue(pageOf([newer, older], 45))
    renderPage()

    expect(await screen.findByText('Page 1 of 3')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))

    expect(await screen.findByText('Page 2 of 3')).toBeInTheDocument()
    expect(lastListCall()).toEqual({ page: 2, pageSize: 20 })
  })
})

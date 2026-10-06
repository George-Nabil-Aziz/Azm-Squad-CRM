import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { listCustomers, type Customer } from '@/api/customers'
import { ApiError } from '@/api/errors'
import { listTicketCategories, type TicketCategory } from '@/api/ticket-categories'
import { createTicket, listTicketAssignees, listTickets, type Ticket } from '@/api/tickets'
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
const signedInViewer: CurrentUser = { ...signedInAgent, id: '7', permissions: [permissions.ticketsView] }

function customer(id: string, name: string): Customer {
  return { id, name, email: null, phone: null, contacts: [], createdAt: '2026-10-01T08:00:00Z', updatedAt: '2026-10-01T08:00:00Z' }
}
const nour = customer('c1', 'Nour Trading')
const omar = customer('c2', 'Omar Walk-in')

const billing: TicketCategory = {
  id: 'k1',
  name: 'Billing',
  isActive: true,
  createdAt: '2026-10-01T08:00:00Z',
  updatedAt: '2026-10-01T08:00:00Z',
}

const created: Ticket = {
  id: 't1',
  number: 'TKT-000001',
  subject: 'Invoice is wrong',
  description: 'Line 3 is charged twice.',
  status: 'new',
  priority: 'high',
  channel: 'manual',
  customerId: 'c1',
  customerName: 'Nour Trading',
  categoryId: 'k1',
  categoryName: 'Billing',
  assigneeId: null,
  assigneeName: null,
  createdAt: '2026-10-01T08:00:00Z',
  updatedAt: '2026-10-01T08:00:00Z',
  firstResponseAt: null,
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

async function openDialog() {
  renderPage()
  fireEvent.click(await screen.findByRole('button', { name: 'New ticket' }))
  return screen.findByRole('dialog', { name: 'New ticket' })
}

function optionsOf(select: HTMLElement) {
  return within(select).getAllByRole('option').map((option) => option.textContent)
}

describe('TicketsPage — new ticket', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(signedInAgent)
    vi.mocked(listCustomers)
      .mockReset()
      .mockResolvedValue({ items: [nour, omar], page: 1, pageSize: 20, totalCount: 2 })
    vi.mocked(listTicketCategories).mockReset().mockResolvedValue([billing])
    vi.mocked(createTicket).mockReset().mockResolvedValue(created)
    vi.mocked(listTickets).mockReset().mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    vi.mocked(listTicketAssignees).mockReset().mockResolvedValue([])
  })

  it('creates a ticket with customer, subject, description, category and priority', async () => {
    const dialog = await openDialog()

    const customerSelect = within(dialog).getByLabelText('Customer')
    await waitFor(() => expect(optionsOf(customerSelect)).toEqual(['Select a customer', 'Nour Trading', 'Omar Walk-in']))
    fireEvent.change(customerSelect, { target: { value: 'c1' } })
    fireEvent.change(within(dialog).getByLabelText('Subject'), { target: { value: ' Invoice is wrong ' } })
    fireEvent.change(within(dialog).getByLabelText('Description'), { target: { value: 'Line 3 is charged twice.' } })
    const categorySelect = within(dialog).getByLabelText('Category')
    await waitFor(() => expect(optionsOf(categorySelect)).toEqual(['No category', 'Billing']))
    fireEvent.change(categorySelect, { target: { value: 'k1' } })
    const prioritySelect = within(dialog).getByLabelText('Priority')
    expect(optionsOf(prioritySelect)).toEqual(['High', 'Mid', 'Low'])
    expect(prioritySelect).toHaveValue('mid')
    fireEvent.change(prioritySelect, { target: { value: 'high' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Create ticket' }))

    await waitFor(() =>
      expect(createTicket).toHaveBeenCalledWith({
        customerId: 'c1',
        subject: 'Invoice is wrong',
        description: 'Line 3 is charged twice.',
        categoryId: 'k1',
        priority: 'high',
      }),
    )
    expect(await screen.findByText('Ticket TKT-000001 was created.')).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('offers only the active categories', async () => {
    await openDialog()

    await waitFor(() => expect(listTicketCategories).toHaveBeenCalledWith({ activeOnly: true }, expect.anything()))
  })

  it('sends no category and no description when none is chosen', async () => {
    const dialog = await openDialog()
    const customerSelect = within(dialog).getByLabelText('Customer')
    await waitFor(() => expect(optionsOf(customerSelect)).toContain('Omar Walk-in'))

    fireEvent.change(customerSelect, { target: { value: 'c2' } })
    fireEvent.change(within(dialog).getByLabelText('Subject'), { target: { value: 'Call back' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Create ticket' }))

    await waitFor(() =>
      expect(createTicket).toHaveBeenCalledWith({
        customerId: 'c2',
        subject: 'Call back',
        description: null,
        categoryId: null,
        priority: 'mid',
      }),
    )
  })

  it('searches customers by name, phone or email', async () => {
    const dialog = await openDialog()
    await waitFor(() => expect(listCustomers).toHaveBeenCalledWith({ search: undefined, page: 1, pageSize: 20 }, expect.anything()))

    fireEvent.change(within(dialog).getByRole('searchbox', { name: 'Search customers' }), { target: { value: ' nour ' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Find' }))

    await waitFor(() => expect(listCustomers).toHaveBeenLastCalledWith({ search: 'nour', page: 1, pageSize: 20 }, expect.anything()))
  })

  it('requires a customer and a subject before calling the API', async () => {
    const dialog = await openDialog()

    fireEvent.click(within(dialog).getByRole('button', { name: 'Create ticket' }))

    expect(await within(dialog).findByText('Choose a customer.')).toBeInTheDocument()
    expect(within(dialog).getByText('Enter a subject.')).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Subject')).toHaveAttribute('aria-invalid', 'true')
    expect(createTicket).not.toHaveBeenCalled()
  })

  it('shows the server message next to the field when the API answers 400', async () => {
    vi.mocked(createTicket).mockRejectedValue(
      new ApiError('POST /api/tickets failed with status 400', 400, {
        status: 400,
        errors: { customerId: ['The customer was not found.'] },
      }),
    )
    const dialog = await openDialog()
    const customerSelect = within(dialog).getByLabelText('Customer')
    await waitFor(() => expect(optionsOf(customerSelect)).toContain('Nour Trading'))
    fireEvent.change(customerSelect, { target: { value: 'c1' } })
    fireEvent.change(within(dialog).getByLabelText('Subject'), { target: { value: 'Hello' } })

    fireEvent.click(within(dialog).getByRole('button', { name: 'Create ticket' }))

    expect(await within(dialog).findByText('The customer was not found.')).toBeInTheDocument()
  })

  it('hides "New ticket" from a user who may only view tickets', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue(signedInViewer)
    renderPage()
    await waitFor(() => expect(getCurrentUser).toHaveBeenCalled())

    expect(screen.getByRole('heading', { level: 1, name: 'Tickets' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'New ticket' })).not.toBeInTheDocument()
  })
})

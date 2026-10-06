import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { ApiError } from '@/api/errors'
import { addTicketMessage, getTicket, listTicketMessages, type Ticket, type TicketMessage } from '@/api/tickets'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { TicketDetailsPage } from './TicketDetailsPage'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))
vi.mock('@/api/tickets', () => ({
  getTicket: vi.fn(),
  listTicketMessages: vi.fn(),
  addTicketMessage: vi.fn(),
}))

const signedInAgent: CurrentUser = {
  id: '2',
  email: 'agent@crm.local',
  fullName: 'Sara Agent',
  roles: ['Agent'],
  permissions: [permissions.customersView, permissions.customersManage, permissions.ticketsView, permissions.ticketsManage],
}

const viewer: CurrentUser = { ...signedInAgent, permissions: [permissions.ticketsView] }

const invoiceTicket: Ticket = {
  id: 't1',
  number: 'TKT-000001',
  subject: 'Invoice is wrong',
  description: 'Line 3 is charged twice.',
  status: 'open',
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
  responseDueAt: null,
  resolutionDueAt: null,
  resolvedAt: null,
  responseBreached: false,
  resolutionBreached: false,
  escalationLevel: 0,
  responseWarnedAt: null,
}

function message(overrides: Partial<TicketMessage>): TicketMessage {
  return {
    id: 'm1',
    direction: 'outbound',
    isInternal: false,
    body: 'We are looking into it.',
    channel: 'manual',
    authorId: '2',
    authorName: 'Sara Agent',
    createdAt: '2026-10-01T09:00:00Z',
    deliveryStatus: null,
    ...overrides,
  }
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter initialEntries={['/tickets/t1']}>
        <Routes>
          <Route path="/tickets/:id" element={<TicketDetailsPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('TicketDetailsPage', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(signedInAgent)
    vi.mocked(getTicket).mockReset().mockResolvedValue(invoiceTicket)
    vi.mocked(listTicketMessages).mockReset().mockResolvedValue([])
    vi.mocked(addTicketMessage).mockReset().mockResolvedValue(message({}))
  })

  it('shows the ticket with its customer, status and description', async () => {
    renderPage()

    expect(await screen.findByRole('heading', { name: 'Invoice is wrong' })).toBeInTheDocument()
    expect(screen.getByText('TKT-000001')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Nour Trading' })).toHaveAttribute('href', '/customers/c1')
    expect(screen.getByText('Open')).toBeInTheDocument()
    expect(screen.getByText('Line 3 is charged twice.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to tickets' })).toHaveAttribute('href', '/tickets')
  })

  it('shows the thread oldest first with author and time', async () => {
    vi.mocked(listTicketMessages).mockResolvedValue([
      message({ id: 'm0', direction: 'inbound', authorId: null, authorName: null, body: 'Where is my refund?', createdAt: '2026-10-01T08:30:00Z' }),
      message({}),
    ])
    renderPage()

    const thread = await screen.findByRole('list', { name: 'Conversation' })
    const items = within(thread).getAllByRole('listitem')
    expect(items).toHaveLength(2)
    expect(items[0]).toHaveTextContent('Where is my refund?')
    expect(items[0]).toHaveTextContent('Nour Trading')
    expect(items[1]).toHaveTextContent('We are looking into it.')
    expect(items[1]).toHaveTextContent('Sara Agent')
    expect(within(items[1]).getByText(/2026/)).toBeInTheDocument()
  })

  it('flags internal notes', async () => {
    vi.mocked(listTicketMessages).mockResolvedValue([
      message({ id: 'm2', direction: 'internal', isInternal: true, body: 'Customer is a VIP.' }),
      message({}),
    ])
    renderPage()

    const items = within(await screen.findByRole('list', { name: 'Conversation' })).getAllByRole('listitem')
    expect(within(items[0]).getByText('Internal note')).toBeInTheDocument()
    expect(within(items[1]).queryByText('Internal note')).not.toBeInTheDocument()
  })

  it('sends a reply to the customer', async () => {
    renderPage()
    await screen.findByRole('heading', { name: 'Invoice is wrong' })

    fireEvent.change(await screen.findByLabelText('Message'), { target: { value: '  Fixed, please check.  ' } })
    fireEvent.click(await screen.findByRole('button', { name: 'Send reply' }))

    await waitFor(() =>
      expect(addTicketMessage).toHaveBeenCalledWith('t1', { body: 'Fixed, please check.', internal: false }),
    )
    await waitFor(() => expect(screen.getByLabelText('Message')).toHaveValue(''))
    expect(listTicketMessages).toHaveBeenCalledTimes(2) // the thread reloads after sending
  })

  it('adds an internal note when the toggle is on', async () => {
    renderPage()
    await screen.findByRole('heading', { name: 'Invoice is wrong' })

    fireEvent.click(await screen.findByRole('checkbox', { name: 'Internal note (the customer never sees it)' }))
    fireEvent.change(await screen.findByLabelText('Message'), { target: { value: 'Customer is a VIP.' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add note' }))

    await waitFor(() => expect(addTicketMessage).toHaveBeenCalledWith('t1', { body: 'Customer is a VIP.', internal: true }))
  })

  it('does not send an empty message', async () => {
    renderPage()
    await screen.findByRole('heading', { name: 'Invoice is wrong' })

    fireEvent.click(await screen.findByRole('button', { name: 'Send reply' }))

    expect(await screen.findByText('Write a message first.')).toBeInTheDocument()
    expect(addTicketMessage).not.toHaveBeenCalled()
  })

  it('shows a server error on the message field', async () => {
    vi.mocked(addTicketMessage).mockRejectedValue(
      new ApiError('bad', 400, { status: 400, errors: { status: ['The ticket is closed. Reopen it to reply or add a note.'] } }),
    )
    renderPage()
    await screen.findByRole('heading', { name: 'Invoice is wrong' })

    fireEvent.change(await screen.findByLabelText('Message'), { target: { value: 'Hello' } })
    fireEvent.click(await screen.findByRole('button', { name: 'Send reply' }))

    expect(await screen.findByText('The ticket is closed. Reopen it to reply or add a note.')).toBeInTheDocument()
  })

  it('has no reply box on a closed ticket', async () => {
    vi.mocked(getTicket).mockResolvedValue({ ...invoiceTicket, status: 'closed' })
    renderPage()

    expect(await screen.findByText('This ticket is closed. Reopen it to reply or add a note.')).toBeInTheDocument()
    expect(screen.queryByLabelText('Message')).not.toBeInTheDocument()
  })

  it('has no reply box without the manage permission', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue(viewer)
    renderPage()

    await screen.findByRole('heading', { name: 'Invoice is wrong' })
    expect(screen.queryByLabelText('Message')).not.toBeInTheDocument()
  })

  it('says when the ticket does not exist', async () => {
    vi.mocked(getTicket).mockRejectedValue(new ApiError('missing', 404))
    renderPage()

    expect(await screen.findByText('The ticket was not found.')).toBeInTheDocument()
  })
})

import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getAiStatus, getTicketClassification, getTicketSummary } from '@/api/ai'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { ApiError } from '@/api/errors'
import { listTicketCategories } from '@/api/ticket-categories'
import {
  addTicketMessage,
  assignTicket,
  changeTicketCategory,
  changeTicketPriority,
  changeTicketStatus,
  getTicketHistory,
  getTicket,
  getTicketCustomerContext,
  listTicketAssignees,
  listTicketMessages,
  type Ticket,
  type TicketHistoryItem,
  type TicketMessage,
} from '@/api/tickets'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { TicketDetailsPage } from './TicketDetailsPage'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))
vi.mock('@/api/ai', () => ({
  getAiStatus: vi.fn().mockResolvedValue({ enabled: false }),
  getTicketSummary: vi.fn(),
  generateReplyDraft: vi.fn(), getSuggestions: vi.fn().mockResolvedValue([]),
  getTicketClassification: vi.fn(),
  generateTicketSummary: vi.fn(),
}))
vi.mock('@/api/ticket-categories', () => ({ listTicketCategories: vi.fn() }))
vi.mock('@/api/tickets', () => ({
  getTicket: vi.fn(),
  listTicketMessages: vi.fn(),
  addTicketMessage: vi.fn(),
  assignTicket: vi.fn(),
  changeTicketStatus: vi.fn(),
  changeTicketCategory: vi.fn(),
  changeTicketPriority: vi.fn(),
  getTicketHistory: vi.fn(),
  getTicketCustomerContext: vi.fn(),
  listTicketAssignees: vi.fn(),
}))

const signedInAgent: CurrentUser = {
  id: '2',
  email: 'agent@crm.local',
  fullName: 'Sara Agent',
  roles: ['Agent'],
  permissions: [permissions.customersView, permissions.customersManage, permissions.ticketsView, permissions.ticketsManage],
}

const viewer: CurrentUser = { ...signedInAgent, permissions: [permissions.ticketsView] }

const signedInSupervisor: CurrentUser = {
  ...signedInAgent,
  id: '3',
  fullName: 'Team Lead',
  roles: ['Supervisor'],
  permissions: [...signedInAgent.permissions, permissions.ticketsAssign],
}

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
  resolvedAt: null,
  allowedStatuses: [],
  responseDueAt: null,
  resolutionDueAt: null,
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
    vi.mocked(getTicketClassification).mockReset().mockResolvedValue({ status: 'none' } as never)
    vi.mocked(getAiStatus).mockReset().mockResolvedValue({ enabled: false })
    vi.mocked(getTicketSummary).mockReset().mockResolvedValue({ text: 'Saved AI summary', language: 'en', generatedAt: '2026-10-01T08:00:00Z' })
    vi.mocked(getTicket).mockReset().mockResolvedValue(invoiceTicket)
    vi.mocked(listTicketMessages).mockReset().mockResolvedValue([])
    vi.mocked(addTicketMessage).mockReset().mockResolvedValue(message({}))
    vi.mocked(listTicketAssignees).mockReset().mockResolvedValue([
      { id: '2', fullName: 'Sara Agent' },
      { id: '4', fullName: 'Omar Agent' },
    ])
    vi.mocked(assignTicket).mockReset().mockResolvedValue({ ...invoiceTicket, assigneeId: '4', assigneeName: 'Omar Agent' })
    vi.mocked(changeTicketStatus).mockReset().mockResolvedValue({ ...invoiceTicket, status: 'pending' })
    vi.mocked(getTicketHistory).mockReset().mockResolvedValue([])
    vi.mocked(getTicketCustomerContext).mockReset().mockResolvedValue({
      customer: { id: 'c1', name: 'Nour Trading', email: null, phone: null, createdAt: '2026-10-01T08:00:00Z', updatedAt: '2026-10-01T08:00:00Z', contacts: [] },
      customerDeleted: false,
      totalTickets: 4,
      recentTickets: [],
    })
    vi.mocked(changeTicketCategory).mockReset().mockResolvedValue(invoiceTicket)
    vi.mocked(changeTicketPriority).mockReset().mockResolvedValue(invoiceTicket)
    vi.mocked(listTicketCategories).mockReset().mockResolvedValue([
      { id: 'k1', name: 'Billing', isActive: true, createdAt: '2026-10-01T08:00:00Z', updatedAt: '2026-10-01T08:00:00Z' },
      { id: 'k2', name: 'Support', isActive: true, createdAt: '2026-10-01T08:00:00Z', updatedAt: '2026-10-01T08:00:00Z' },
    ])
  })

  it('shows the customer panel to a user who may see customers, and not to one who may not', async () => {
    const { unmount } = renderPage()
    expect(await screen.findByText('Total tickets: 4')).toBeInTheDocument()
    unmount()

    vi.mocked(getCurrentUser).mockResolvedValue(viewer)
    renderPage()
    await screen.findByRole('heading', { level: 1, name: 'Invoice is wrong' })
    await waitFor(() => expect(getCurrentUser).toHaveBeenCalledTimes(2))

    expect(screen.queryByText('Total tickets: 4')).not.toBeInTheDocument()
  })

  it('shows the AI summary panel only when AI is configured', async () => {
    const { unmount } = renderPage()
    await screen.findByRole('heading', { name: 'Invoice is wrong' })
    await waitFor(() => expect(getAiStatus).toHaveBeenCalled())
    expect(screen.queryByRole('region', { name: 'AI summary' })).not.toBeInTheDocument()
    unmount()

    vi.mocked(getAiStatus).mockResolvedValue({ enabled: true })
    renderPage()

    expect(await screen.findByRole('region', { name: 'AI summary' })).toBeInTheDocument()
    expect(await screen.findByText('Saved AI summary')).toBeInTheDocument()
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

  it('mentions a colleague in an internal note and sends the id', async () => {
    renderPage()
    await screen.findByRole('heading', { name: 'Invoice is wrong' })
    expect(screen.queryByLabelText('Mention a colleague')).not.toBeInTheDocument() // replies to the customer cannot mention anybody

    fireEvent.click(await screen.findByRole('checkbox', { name: 'Internal note (the customer never sees it)' }))
    await waitFor(() => expect(listTicketAssignees).toHaveBeenCalled())
    const select = await screen.findByLabelText('Mention a colleague')
    await screen.findByRole('option', { name: 'Omar Agent' })
    fireEvent.change(select, { target: { value: '4' } })
    expect(screen.getByLabelText('Message')).toHaveValue('@Omar Agent ')
    fireEvent.change(screen.getByLabelText('Message'), { target: { value: '@Omar Agent please check the invoice' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add note' }))

    await waitFor(() =>
      expect(addTicketMessage).toHaveBeenCalledWith('t1', {
        body: '@Omar Agent please check the invoice',
        internal: true,
        mentionedUserIds: ['4'],
      }),
    )
  })

  it('does not send a mention whose @name was removed from the note', async () => {
    renderPage()
    await screen.findByRole('heading', { name: 'Invoice is wrong' })
    fireEvent.click(await screen.findByRole('checkbox', { name: 'Internal note (the customer never sees it)' }))
    await screen.findByRole('option', { name: 'Omar Agent' })
    fireEvent.change(await screen.findByLabelText('Mention a colleague'), { target: { value: '4' } })

    fireEvent.change(screen.getByLabelText('Message'), { target: { value: 'never mind' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add note' }))

    await waitFor(() => expect(addTicketMessage).toHaveBeenCalledWith('t1', { body: 'never mind', internal: true }))
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

  it('offers a template field when the server refuses free text (WhatsApp 24-hour window)', async () => {
    vi.mocked(addTicketMessage).mockRejectedValueOnce(
      new ApiError('bad', 400, { status: 400, errors: { body: ['The last customer message is older than 24 hours. Send an approved template instead.'] } }),
    )
    renderPage()
    await screen.findByRole('heading', { name: 'Invoice is wrong' })

    fireEvent.change(await screen.findByLabelText('Message'), { target: { value: 'Late answer' } })
    fireEvent.click(await screen.findByRole('button', { name: 'Send reply' }))
    expect(
      await screen.findByText('The last customer message is older than 24 hours. Send an approved template instead.'),
    ).toBeInTheDocument()

    fireEvent.change(await screen.findByLabelText(/Approved WhatsApp template name/), { target: { value: 'order_update' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send reply' }))

    await waitFor(() =>
      expect(addTicketMessage).toHaveBeenLastCalledWith('t1', { body: 'Late answer', internal: false, templateName: 'order_update' }),
    )
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

  describe('assigning', () => {
    it('lets a supervisor choose an agent and assign', async () => {
      vi.mocked(getCurrentUser).mockResolvedValue(signedInSupervisor)
      renderPage()

      const select = await screen.findByLabelText('Assign to')
      expect(within(select).getAllByRole('option').map((option) => option.textContent)).toEqual([
        'Unassigned',
        'Sara Agent',
        'Omar Agent',
      ])
      fireEvent.change(select, { target: { value: '4' } })
      fireEvent.click(screen.getByRole('button', { name: 'Assign' }))

      await waitFor(() => expect(assignTicket).toHaveBeenCalledWith('t1', '4'))
    })

    it('unassigns when "Unassigned" is chosen', async () => {
      vi.mocked(getCurrentUser).mockResolvedValue(signedInSupervisor)
      vi.mocked(getTicket).mockResolvedValue({ ...invoiceTicket, assigneeId: '4', assigneeName: 'Omar Agent' })
      renderPage()

      fireEvent.change(await screen.findByLabelText('Assign to'), { target: { value: '' } })
      fireEvent.click(screen.getByRole('button', { name: 'Assign' }))

      await waitFor(() => expect(assignTicket).toHaveBeenCalledWith('t1', null))
    })

    it('shows the server message when the assignment is refused', async () => {
      vi.mocked(getCurrentUser).mockResolvedValue(signedInSupervisor)
      vi.mocked(assignTicket).mockRejectedValue(
        new ApiError('bad', 400, { status: 400, errors: { assigneeId: ['Choose an active staff user.'] } }),
      )
      renderPage()

      fireEvent.change(await screen.findByLabelText('Assign to'), { target: { value: '4' } })
      fireEvent.click(screen.getByRole('button', { name: 'Assign' }))

      expect(await screen.findByText('Choose an active staff user.')).toBeInTheDocument()
    })

    it('gives an agent without the assign permission only "Assign to me"', async () => {
      renderPage()

      fireEvent.click(await screen.findByRole('button', { name: 'Assign to me' }))

      expect(screen.queryByLabelText('Assign to')).not.toBeInTheDocument()
      await waitFor(() => expect(assignTicket).toHaveBeenCalledWith('t1', '2'))
    })

    it('hides "Assign to me" when the ticket is already the agent\'s', async () => {
      vi.mocked(getTicket).mockResolvedValue({ ...invoiceTicket, assigneeId: '2', assigneeName: 'Sara Agent' })
      renderPage()

      await screen.findByLabelText('Message')
      expect(screen.queryByRole('button', { name: 'Assign to me' })).not.toBeInTheDocument()
    })

    it('shows no assign controls to a viewer', async () => {
      vi.mocked(getCurrentUser).mockResolvedValue(viewer)
      renderPage()

      await screen.findByRole('heading', { name: 'Invoice is wrong' })
      expect(screen.queryByLabelText('Assign to')).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: 'Assign to me' })).not.toBeInTheDocument()
    })
  })

  describe('status workflow', () => {
    function withStatus(status: Ticket['status'], allowedStatuses: Ticket['allowedStatuses'], resolvedAt: string | null = null) {
      vi.mocked(getTicket).mockResolvedValue({ ...invoiceTicket, status, allowedStatuses, resolvedAt })
    }

    it('offers only the allowed moves of an open ticket', async () => {
      withStatus('open', ['pending', 'resolved'])
      renderPage()

      expect(await screen.findByRole('button', { name: 'Set pending' })).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Resolve' })).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: 'Close' })).not.toBeInTheDocument()
    })

    it('sends the chosen status', async () => {
      withStatus('open', ['pending', 'resolved'])
      renderPage()

      fireEvent.click(await screen.findByRole('button', { name: 'Set pending' }))

      await waitFor(() => expect(changeTicketStatus).toHaveBeenCalledWith('t1', 'pending'))
    })

    it('offers Close and Reopen on a resolved ticket and shows when it was resolved', async () => {
      withStatus('resolved', ['open', 'closed'], '2026-10-02T10:00:00Z')
      renderPage()

      expect(await screen.findByRole('button', { name: 'Close' })).toBeInTheDocument()
      fireEvent.click(screen.getByRole('button', { name: 'Reopen' }))

      await waitFor(() => expect(changeTicketStatus).toHaveBeenCalledWith('t1', 'open'))
      expect(screen.getByText('Resolved at')).toBeInTheDocument()
    })

    it('offers only Reopen on a closed ticket', async () => {
      withStatus('closed', ['open'])
      renderPage()

      expect(await screen.findByRole('button', { name: 'Reopen' })).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: 'Close' })).not.toBeInTheDocument()
    })

    it('shows the server message when the move is refused', async () => {
      withStatus('open', ['pending', 'resolved'])
      vi.mocked(changeTicketStatus).mockRejectedValue(
        new ApiError('bad', 400, { status: 400, errors: { status: ['A closed ticket can only move to: open.'] } }),
      )
      renderPage()

      fireEvent.click(await screen.findByRole('button', { name: 'Resolve' }))

      expect(await screen.findByText('A closed ticket can only move to: open.')).toBeInTheDocument()
    })

    it('shows no status actions without the manage permission', async () => {
      vi.mocked(getCurrentUser).mockResolvedValue(viewer)
      withStatus('open', ['pending', 'resolved'])
      renderPage()

      await screen.findByRole('heading', { name: 'Invoice is wrong' })
      expect(screen.queryByRole('button', { name: 'Resolve' })).not.toBeInTheDocument()
    })
  })

  describe('history', () => {
    const entries: TicketHistoryItem[] = [
      { id: 'h1', field: 'status', oldValue: 'new', newValue: 'open', changedById: '3', changedByName: 'Team Lead', changedAt: '2026-10-01T09:00:00Z' },
      { id: 'h2', field: 'assignee', oldValue: null, newValue: 'Sara Agent', changedById: '3', changedByName: 'Team Lead', changedAt: '2026-10-01T10:00:00Z' },
      { id: 'h3', field: 'priority', oldValue: 'low', newValue: 'high', changedById: '3', changedByName: 'Team Lead', changedAt: '2026-10-01T11:00:00Z' },
      { id: 'h4', field: 'category', oldValue: 'Billing', newValue: null, changedById: '3', changedByName: 'Team Lead', changedAt: '2026-10-01T12:00:00Z' },
      { id: 's1', field: 'escalation', oldValue: null, newValue: '1', changedById: null, changedByName: null, changedAt: '2026-10-01T13:00:00Z' },
    ]

    async function openHistory() {
      renderPage()
      fireEvent.click(await screen.findByRole('tab', { name: 'History' }))
      return within(await screen.findByRole('list', { name: 'History' })).getAllByRole('listitem')
    }

    it('lists the entries in the order returned, with old and new value, user and time', async () => {
      vi.mocked(getTicketHistory).mockResolvedValue(entries)

      const items = await openHistory()

      expect(items).toHaveLength(5)
      expect(items[0]).toHaveTextContent('Status')
      expect(items[0]).toHaveTextContent('New → Open')
      expect(items[0]).toHaveTextContent('Team Lead')
      expect(within(items[0]).getByText(/2026/)).toBeInTheDocument()
      expect(items[1]).toHaveTextContent('None → Sara Agent')
      expect(items[2]).toHaveTextContent('Low → High')
      expect(items[3]).toHaveTextContent('Billing → None')
      expect(getTicketHistory).toHaveBeenCalledWith('t1', expect.anything())
    })

    it('shows an SLA escalation as a system entry with its level', async () => {
      vi.mocked(getTicketHistory).mockResolvedValue(entries)

      const items = await openHistory()

      expect(items[4]).toHaveTextContent('Escalated to level 1')
      expect(items[4]).toHaveTextContent('System')
    })

    it('says when nothing changed yet', async () => {
      renderPage()
      fireEvent.click(await screen.findByRole('tab', { name: 'History' }))

      expect(await screen.findByText('No changes recorded yet.')).toBeInTheDocument()
    })

    it('has no way to edit or delete an entry', async () => {
      vi.mocked(getTicketHistory).mockResolvedValue(entries)

      const items = await openHistory()

      expect(within(items[0]).queryByRole('button')).not.toBeInTheDocument()
    })
  })

  describe('priority and category', () => {
    it('sends a new priority', async () => {
      renderPage()

      fireEvent.change(await screen.findByLabelText('Priority'), { target: { value: 'low' } })

      await waitFor(() => expect(changeTicketPriority).toHaveBeenCalledWith('t1', 'low'))
    })

    it('sends a new category, or none', async () => {
      renderPage()

      fireEvent.change(await screen.findByLabelText('Category'), { target: { value: 'k2' } })
      await waitFor(() => expect(changeTicketCategory).toHaveBeenCalledWith('t1', 'k2'))
      fireEvent.change(screen.getByLabelText('Category'), { target: { value: '' } })
      await waitFor(() => expect(changeTicketCategory).toHaveBeenLastCalledWith('t1', null))
    })

    it('is hidden without the manage permission', async () => {
      vi.mocked(getCurrentUser).mockResolvedValue(viewer)
      renderPage()

      await screen.findByRole('heading', { name: 'Invoice is wrong' })
      expect(screen.queryByLabelText('Priority')).not.toBeInTheDocument()
    })
  })
})

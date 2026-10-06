import { QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getTicketCustomerContext, type TicketCustomerContext } from '@/api/tickets'
import { createQueryClient } from '@/app/query-client'
import { CustomerPanel } from './CustomerPanel'

vi.mock('@/api/tickets', () => ({ getTicketCustomerContext: vi.fn() }))

const context: TicketCustomerContext = {
  customer: {
    id: 'c1',
    name: 'Nour Trading',
    email: 'info@nour.example',
    phone: '+966501234567',
    createdAt: '2026-10-01T08:00:00Z',
    updatedAt: '2026-10-01T08:00:00Z',
    contacts: [
      { id: 'p1', type: 'phone', value: '+966501234567', isPrimary: true },
      { id: 'e1', type: 'email', value: 'info@nour.example', isPrimary: true },
    ],
  },
  customerDeleted: false,
  totalTickets: 7,
  recentTickets: [
    { id: 't7', number: 'TKT-000007', subject: 'Printer is down', status: 'new', priority: 'high', createdAt: '2026-10-05T08:00:00Z', isCurrent: false },
    { id: 't6', number: 'TKT-000006', subject: 'Invoice is wrong', status: 'pending', priority: 'mid', createdAt: '2026-10-04T08:00:00Z', isCurrent: true },
  ],
}

function renderPanel(ticketId = 't6', customerId = 'c1') {
  const client = createQueryClient()
  const view = (id: string, customer: string) => (
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <CustomerPanel ticketId={id} customerId={customer} />
      </MemoryRouter>
    </QueryClientProvider>
  )
  const result = render(view(ticketId, customerId))
  return { ...result, rerenderFor: (id: string, customer: string) => result.rerender(view(id, customer)) }
}

describe('CustomerPanel', () => {
  beforeEach(() => {
    vi.mocked(getTicketCustomerContext).mockReset().mockResolvedValue(context)
  })

  it('shows the customer name, contact details and total ticket count', async () => {
    renderPanel()

    await screen.findByText('Nour Trading')
    const panel = screen.getByRole('complementary', { name: 'Customer' })
    expect(within(panel).getByText('Nour Trading')).toBeInTheDocument()
    expect(within(panel).getByText('+966501234567')).toBeInTheDocument()
    expect(within(panel).getByText('info@nour.example')).toBeInTheDocument()
    expect(within(panel).getByText('Total tickets: 7')).toBeInTheDocument()
  })

  it('lists the last tickets with their status, linking to each', async () => {
    renderPanel()

    const list = await screen.findByRole('list', { name: 'Recent tickets' })
    const items = within(list).getAllByRole('listitem')
    expect(items).toHaveLength(2)
    expect(within(items[0]).getByRole('link', { name: 'TKT-000007' })).toHaveAttribute('href', '/tickets/t7')
    expect(within(items[0]).getByText('New')).toBeInTheDocument()
    expect(within(items[1]).getByText('Pending')).toBeInTheDocument()
    expect(within(items[1]).getByText('This ticket')).toBeInTheDocument()
  })

  it('links to the full customer profile', async () => {
    renderPanel()

    expect(await screen.findByRole('link', { name: 'Open customer profile' })).toHaveAttribute('href', '/customers/c1')
  })

  it('loads the new customer when the ticket moves to another customer', async () => {
    const { rerenderFor } = renderPanel()
    await screen.findByText('Nour Trading')
    vi.mocked(getTicketCustomerContext).mockResolvedValue({
      ...context,
      customer: { ...context.customer, id: 'c2', name: 'Al Amal Clinic', contacts: [] },
      totalTickets: 1,
      recentTickets: [],
    })

    rerenderFor('t6', 'c2')

    expect(await screen.findByText('Al Amal Clinic')).toBeInTheDocument()
    expect(screen.getByText('Total tickets: 1')).toBeInTheDocument()
    expect(screen.getByText('No contact details.')).toBeInTheDocument()
    await waitFor(() => expect(getTicketCustomerContext).toHaveBeenCalledTimes(2))
  })

  it('marks a deleted customer and hides the profile link', async () => {
    vi.mocked(getTicketCustomerContext).mockResolvedValue({ ...context, customerDeleted: true })
    renderPanel()

    expect(await screen.findByText('This customer was deleted.')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Open customer profile' })).not.toBeInTheDocument()
  })
})

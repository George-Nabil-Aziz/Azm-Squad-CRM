import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import {
  getCustomer,
  getCustomerTimeline,
  listCustomerAttachments,
  listCustomerNotes,
  type Customer,
  type CustomerInteraction,
} from '@/api/customers'
import { ApiError } from '@/api/errors'
import type { PagedResult } from '@/api/paging'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { CustomerDetailsPage } from './CustomerDetailsPage'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))

vi.mock('@/api/customers', () => ({
  getCustomer: vi.fn(),
  getCustomerTimeline: vi.fn(),
  listCustomerNotes: vi.fn(),
  listCustomerAttachments: vi.fn(),
  makeCustomerContactPrimary: vi.fn(),
  removeCustomerContact: vi.fn(),
}))

const signedInAgent: CurrentUser = {
  id: '2',
  email: 'agent@crm.local',
  fullName: 'Sara Agent',
  roles: ['Agent'],
  permissions: [permissions.customersView, permissions.customersManage],
}

const nour: Customer = {
  id: 'c1',
  name: 'Nour Trading',
  email: 'info@nour.example',
  phone: '+966501234567',
  createdAt: '2026-10-01T08:00:00Z',
  updatedAt: '2026-10-01T08:00:00Z',
  contacts: [{ id: 'k1', type: 'phone', value: '+966501234567', isPrimary: true }],
}

function entry(id: number, event: string, details: string | null, actorName: string | null, type = 'customer') {
  return {
    id,
    type,
    event,
    details,
    sourceId: null,
    actorId: actorName ? 'u2' : null,
    actorName,
    occurredAt: `2026-10-0${id}T08:00:00Z`,
  } as CustomerInteraction
}

function timelinePage(items: CustomerInteraction[], totalCount = items.length, page = 1): PagedResult<CustomerInteraction> {
  return { items, page, pageSize: 10, totalCount }
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter initialEntries={['/customers/c1']}>
        <Routes>
          <Route path="/customers/:id" element={<CustomerDetailsPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

async function timelineEntries() {
  const list = await screen.findByRole('list', { name: 'Interaction history' })
  return within(list).getAllByRole('listitem')
}

describe('CustomerDetailsPage', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(signedInAgent)
    vi.mocked(getCustomer).mockReset().mockResolvedValue(nour)
    vi.mocked(listCustomerNotes).mockReset().mockResolvedValue({ items: [], page: 1, pageSize: 10, totalCount: 0 })
    vi.mocked(listCustomerAttachments).mockReset().mockResolvedValue([])
    vi.mocked(getCustomerTimeline)
      .mockReset()
      .mockResolvedValue(
        timelinePage([
          entry(3, 'customerUpdated', 'Nour Trading', 'Sara Agent'),
          entry(2, 'contactAdded', '+966501234567', 'Sara Agent'),
          entry(1, 'customerCreated', 'Nour Trading', 'Sara Agent'),
        ]),
      )
  })

  it('shows the customer and the timeline newest first', async () => {
    renderPage()

    expect(await screen.findByRole('heading', { name: 'Nour Trading' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to customers' })).toHaveAttribute('href', '/customers')
    const items = await timelineEntries()
    expect(items.map((item) => within(item).getByRole('heading').textContent)).toEqual([
      'Customer updated',
      'Contact added',
      'Customer created',
    ])
    expect(items[1]).toHaveTextContent('+966501234567')
    expect(items[0]).toHaveTextContent('by Sara Agent')
    expect(getCustomerTimeline).toHaveBeenCalledWith('c1', { page: 1, pageSize: 10 }, expect.anything())
  })

  it('filters the timeline by type', async () => {
    renderPage()
    await timelineEntries()

    fireEvent.change(screen.getByLabelText('Show'), { target: { value: 'note' } })

    await waitFor(() =>
      expect(getCustomerTimeline).toHaveBeenLastCalledWith('c1', { type: 'note', page: 1, pageSize: 10 }, expect.anything()),
    )
  })

  it('pages through the timeline', async () => {
    vi.mocked(getCustomerTimeline).mockImplementation(async (_id, params) =>
      timelinePage([entry(params.page === 2 ? 1 : 3, 'customerUpdated', `page ${params.page}`, 'Sara Agent')], 12, params.page),
    )
    renderPage()
    await timelineEntries()

    expect(screen.getByText('Page 1 of 2')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))

    expect(await screen.findByText('page 2')).toBeInTheDocument()
    expect(screen.getByText('Page 2 of 2')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()
  })

  it('shows a system entry without an actor and unknown events as other activity', async () => {
    vi.mocked(getCustomerTimeline).mockResolvedValue(
      timelinePage([entry(1, 'somethingNew', 'Hello', null, 'message')]),
    )
    renderPage()

    const [item] = await timelineEntries()

    expect(within(item).getByRole('heading')).toHaveTextContent('Other activity')
    expect(item).toHaveTextContent('System')
  })

  it('labels note and attachment entries', async () => {
    vi.mocked(getCustomerTimeline).mockResolvedValue(
      timelinePage([
        entry(2, 'attachmentAdded', 'report.pdf', 'Sara Agent', 'attachment'),
        entry(1, 'noteAdded', 'Prefers WhatsApp.', 'Sara Agent', 'note'),
      ]),
    )
    renderPage()

    const items = await timelineEntries()

    expect(items.map((item) => within(item).getByRole('heading').textContent)).toEqual(['File attached', 'Note added'])
  })

  it('labels ticket entries (CRM-13)', async () => {
    vi.mocked(getCustomerTimeline).mockResolvedValue(
      timelinePage([entry(1, 'ticketCreated', 'TKT-000001 Cannot log in', 'Sara Agent', 'ticket')]),
    )
    renderPage()

    const [item] = await timelineEntries()

    expect(within(item).getByRole('heading')).toHaveTextContent('Ticket created')
    expect(item).toHaveTextContent('TKT-000001 Cannot log in')
  })

  it('says when nothing happened yet', async () => {
    vi.mocked(getCustomerTimeline).mockResolvedValue(timelinePage([]))
    renderPage()

    expect(await screen.findByText('Nothing has happened yet.')).toBeInTheDocument()
  })

  it('shows not found for an unknown customer', async () => {
    vi.mocked(getCustomer).mockRejectedValue(new ApiError('GET /api/customers/c1 failed with status 404', 404))
    renderPage()

    expect(await screen.findByText('Customer not found.')).toBeInTheDocument()
  })
})

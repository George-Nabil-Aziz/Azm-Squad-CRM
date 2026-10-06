import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from '@/App'
import { clearPortalSession, savePortalSession } from '@/auth/portal-session'

const inOneHour = () => new Date(Date.now() + 3_600_000).toISOString()

function json(status: number, body: unknown, type = 'application/json') {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': type } })
}

const ticket = {
  id: 't1',
  number: 'TKT-000007',
  subject: 'Printer is broken',
  description: 'Blank pages',
  status: 'open',
  categoryName: 'Billing',
  createdAt: '2026-10-01T08:00:00Z',
  updatedAt: '2026-10-02T08:00:00Z',
  canReply: true,
  canReopen: false,
}

let current = { ...ticket }
const posts: { path: string; body: unknown }[] = []

function stubApi() {
  const fetchMock = vi.fn(async (path: string, init?: RequestInit) => {
    if (init?.method === 'POST') posts.push({ path, body: init.body ? JSON.parse(String(init.body)) : undefined })
    if (path.startsWith('/api/portal/tickets?')) return json(200, { items: [current], page: 1, pageSize: 20, totalCount: 1 })
    if (path === '/api/portal/tickets/t1') return json(200, current)
    if (path === '/api/portal/tickets/t1/messages' && init?.method === 'POST')
      return json(201, { id: 'm9', fromCustomer: true, authorName: null, body: 'Any news?', createdAt: '2026-10-03T08:00:00Z' })
    if (path === '/api/portal/tickets/t1/messages')
      return json(200, [
        { id: 'm1', fromCustomer: true, authorName: null, body: 'It prints blank', createdAt: '2026-10-01T08:00:00Z' },
        { id: 'm2', fromCustomer: false, authorName: 'Sara Agent', body: 'Try restarting it', createdAt: '2026-10-01T09:00:00Z' },
      ])
    if (path === '/api/portal/tickets/t1/history')
      return json(200, [
        { type: 'created', status: null, at: '2026-10-01T08:00:00Z' },
        { type: 'status', status: 'open', at: '2026-10-01T09:00:00Z' },
      ])
    if (path === '/api/portal/tickets/t1/reopen') {
      current = { ...current, status: 'open', canReopen: false, canReply: true }
      return json(200, current)
    }
    return json(404, { status: 404 }, 'application/problem+json')
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function renderAt(path: string) {
  window.history.replaceState(null, '', path)
  return render(<App />)
}

describe('Portal tracking', () => {
  beforeEach(() => {
    current = { ...ticket }
    posts.length = 0
    savePortalSession('portal-token', inOneHour(), { id: 'c1', name: 'Nour', email: 'n@x.example' })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    clearPortalSession()
    localStorage.clear()
  })

  it('lists the customer requests with their status', async () => {
    stubApi()
    renderAt('/portal/tickets')

    const link = await screen.findByRole('link', { name: 'Printer is broken' })
    expect(link).toHaveAttribute('href', '/portal/tickets/t1')
    expect(screen.getByText('Open')).toBeInTheDocument()
    expect(screen.getByText('TKT-000007')).toBeInTheDocument()
  })

  it('shows the status, the public conversation and the history of a request', async () => {
    stubApi()
    renderAt('/portal/tickets/t1')

    expect(await screen.findByRole('heading', { level: 1, name: 'Printer is broken' })).toBeInTheDocument()
    const conversation = await screen.findByRole('region', { name: 'Conversation' })
    expect(await within(conversation).findByText('Try restarting it')).toBeInTheDocument()
    expect(within(conversation).getByText(/Sara Agent/)).toBeInTheDocument()
    const history = screen.getByRole('region', { name: 'History' })
    expect(await within(history).findByText(/Request created/)).toBeInTheDocument()
    expect(within(history).getByText(/Status changed to Open/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reopen request' })).not.toBeInTheDocument()
  })

  it('sends a reply on an open request', async () => {
    stubApi()
    renderAt('/portal/tickets/t1')
    fireEvent.change(await screen.findByLabelText('Your reply'), { target: { value: 'Any news?' } })

    fireEvent.click(screen.getByRole('button', { name: 'Send reply' }))

    await waitFor(() => expect(posts).toContainEqual({ path: '/api/portal/tickets/t1/messages', body: { body: 'Any news?' } }))
  })

  it('asks for text before replying', async () => {
    stubApi()
    renderAt('/portal/tickets/t1')

    fireEvent.click(await screen.findByRole('button', { name: 'Send reply' }))

    expect(await screen.findByText('Write a message.')).toBeInTheDocument()
    expect(posts).toHaveLength(0)
  })

  it('offers to reopen a resolved request inside the allowed days, and hides the reply box', async () => {
    current = { ...ticket, status: 'resolved', canReply: false, canReopen: true }
    stubApi()
    renderAt('/portal/tickets/t1')
    expect(await screen.findByRole('button', { name: 'Reopen request' })).toBeInTheDocument()
    expect(screen.queryByLabelText('Your reply')).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Reopen request' }))

    await waitFor(() => expect(posts).toContainEqual({ path: '/api/portal/tickets/t1/reopen', body: {} }))
    expect(await screen.findByLabelText('Your reply')).toBeInTheDocument()
  })

  it('says so when the request is not found (another customer or unknown)', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => json(404, { status: 404 }, 'application/problem+json')))
    renderAt('/portal/tickets/zzz')

    expect(await screen.findByText('This request was not found.')).toBeInTheDocument()
  })
})

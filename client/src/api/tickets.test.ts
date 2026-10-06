import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  addTicketMessage,
  assignTicket,
  changeTicketStatus,
  changeTicketPriority,
  createTicket,
  getTicket,
  listTicketAssignees,
  listTicketMessages,
  listTickets,
} from './tickets'

function fakeFetch(status = 200, body: unknown = {}) {
  const fetchMock = vi.fn().mockResolvedValue(
    new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function sent(fetchMock: ReturnType<typeof vi.fn>) {
  const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit]
  return { path, method: init.method, body: init.body === undefined ? undefined : JSON.parse(String(init.body)) }
}

describe('tickets API', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('creates a ticket with POST /api/tickets', async () => {
    const fetchMock = fakeFetch(201, { id: 't1', number: 'TKT-000001' })
    const request = {
      customerId: 'c1',
      subject: 'Invoice is wrong',
      description: 'Line 3 is charged twice.',
      categoryId: 'k1',
      priority: 'high' as const,
    }

    const ticket = await createTicket(request)

    expect(sent(fetchMock)).toEqual({ path: '/api/tickets', method: 'POST', body: request })
    expect(ticket.number).toBe('TKT-000001')
  })

  it('reads one ticket with GET /api/tickets/{id}', async () => {
    const fetchMock = fakeFetch(200, { id: 't1' })

    await getTicket('t1')

    expect(sent(fetchMock)).toMatchObject({ path: '/api/tickets/t1', method: 'GET' })
  })

  it('lists tickets without a query string by default', async () => {
    const fetchMock = fakeFetch(200, { items: [], page: 1, pageSize: 20, totalCount: 0 })

    await listTickets({})

    expect(sent(fetchMock)).toMatchObject({ path: '/api/tickets', method: 'GET' })
  })

  it('sends every filter, the search and paging in the query string', async () => {
    const fetchMock = fakeFetch(200, { items: [], page: 2, pageSize: 20, totalCount: 0 })

    await listTickets({
      status: 'open',
      priority: 'high',
      categoryId: 'k1',
      assigneeId: 'u1',
      createdFrom: '2026-10-01',
      createdTo: '2026-10-05',
      search: 'TKT-1',
      page: 2,
      pageSize: 20,
    })

    expect(sent(fetchMock).path).toBe(
      '/api/tickets?status=open&priority=high&categoryId=k1&assigneeId=u1&createdFrom=2026-10-01&createdTo=2026-10-05&search=TKT-1&page=2&pageSize=20',
    )
  })

  it('asks for unassigned tickets', async () => {
    const fetchMock = fakeFetch(200, { items: [], page: 1, pageSize: 20, totalCount: 0 })

    await listTickets({ unassigned: true, page: 1 })

    expect(sent(fetchMock).path).toBe('/api/tickets?unassigned=true&page=1')
  })

  it('changes the priority with PUT /api/tickets/{id}/priority', async () => {
    const fetchMock = fakeFetch(200, {})

    await changeTicketPriority('t1', 'high')

    expect(sent(fetchMock)).toMatchObject({ path: '/api/tickets/t1/priority', method: 'PUT' })
  })

  it('lists the assignees with GET /api/tickets/assignees', async () => {
    const fetchMock = fakeFetch(200, [])

    await listTicketAssignees()

    expect(sent(fetchMock)).toMatchObject({ path: '/api/tickets/assignees', method: 'GET' })
  })

  it('lists the thread with GET /api/tickets/{id}/messages', async () => {
    const fetchMock = fakeFetch(200, [])

    await listTicketMessages('t1')

    expect(sent(fetchMock)).toMatchObject({ path: '/api/tickets/t1/messages', method: 'GET' })
  })

  it('sends a reply with POST /api/tickets/{id}/messages', async () => {
    const fetchMock = fakeFetch(201, { id: 'm1' })

    await addTicketMessage('t1', { body: 'We are on it.', internal: false })

    expect(sent(fetchMock)).toEqual({
      path: '/api/tickets/t1/messages',
      method: 'POST',
      body: { body: 'We are on it.', internal: false },
    })
  })

  it('assigns a ticket with POST /api/tickets/{id}/assign', async () => {
    const fetchMock = fakeFetch(200, { id: 't1' })

    await assignTicket('t1', 'u1')

    expect(sent(fetchMock)).toEqual({ path: '/api/tickets/t1/assign', method: 'POST', body: { assigneeId: 'u1' } })
  })

  it('unassigns a ticket by sending a null assignee', async () => {
    const fetchMock = fakeFetch(200, { id: 't1' })

    await assignTicket('t1', null)

    expect(sent(fetchMock).body).toEqual({ assigneeId: null })
  })

  it('changes the status with PUT /api/tickets/{id}/status', async () => {
    const fetchMock = fakeFetch(200, { id: 't1' })

    await changeTicketStatus('t1', 'resolved')

    expect(sent(fetchMock)).toEqual({ path: '/api/tickets/t1/status', method: 'PUT', body: { status: 'resolved' } })
  })
})

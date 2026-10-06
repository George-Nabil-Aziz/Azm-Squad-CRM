import { afterEach, describe, expect, it, vi } from 'vitest'
import { createTicket, getTicket, listTicketAssignees, listTickets } from './tickets'

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

  it('lists the assignees with GET /api/tickets/assignees', async () => {
    const fetchMock = fakeFetch(200, [])

    await listTicketAssignees()

    expect(sent(fetchMock)).toMatchObject({ path: '/api/tickets/assignees', method: 'GET' })
  })
})

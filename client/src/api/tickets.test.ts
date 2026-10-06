import { afterEach, describe, expect, it, vi } from 'vitest'
import { createTicket, getTicket } from './tickets'

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
})

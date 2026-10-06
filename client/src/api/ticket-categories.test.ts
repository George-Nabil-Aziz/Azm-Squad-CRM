import { afterEach, describe, expect, it, vi } from 'vitest'
import { createTicketCategory, listTicketCategories, updateTicketCategory } from './ticket-categories'

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

describe('ticket categories API', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lists every category by default', async () => {
    const fetchMock = fakeFetch(200, [])

    await listTicketCategories({})

    expect(sent(fetchMock)).toMatchObject({ path: '/api/ticket-categories', method: 'GET' })
  })

  it('asks for the active categories only', async () => {
    const fetchMock = fakeFetch(200, [])

    await listTicketCategories({ activeOnly: true })

    expect(sent(fetchMock).path).toBe('/api/ticket-categories?activeOnly=true')
  })

  it('creates a category with POST /api/ticket-categories', async () => {
    const fetchMock = fakeFetch(201, { id: 'k1' })

    await createTicketCategory({ name: 'Billing', isActive: true })

    expect(sent(fetchMock)).toEqual({
      path: '/api/ticket-categories',
      method: 'POST',
      body: { name: 'Billing', isActive: true },
    })
  })

  it('updates a category with PUT /api/ticket-categories/{id}', async () => {
    const fetchMock = fakeFetch(200, { id: 'k1' })

    await updateTicketCategory('k1', { name: 'Invoices', isActive: false })

    expect(sent(fetchMock)).toEqual({
      path: '/api/ticket-categories/k1',
      method: 'PUT',
      body: { name: 'Invoices', isActive: false },
    })
  })
})

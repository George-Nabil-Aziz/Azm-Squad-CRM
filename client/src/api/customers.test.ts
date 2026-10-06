import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  addCustomerContact,
  createCustomer,
  deleteCustomer,
  getCustomer,
  listCustomers,
  makeCustomerContactPrimary,
  removeCustomerContact,
  updateCustomer,
} from './customers'

function fakeFetch(status = 200, body: unknown = {}) {
  const fetchMock = vi.fn().mockResolvedValue(
    status === 204
      ? new Response(null, { status })
      : new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function sent(fetchMock: ReturnType<typeof vi.fn>) {
  const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit]
  return { path, method: init.method, body: init.body === undefined ? undefined : JSON.parse(String(init.body)) }
}

describe('customers API', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lists customers without a query string by default', async () => {
    const fetchMock = fakeFetch(200, { items: [], page: 1, pageSize: 20, totalCount: 0 })

    await listCustomers({})

    expect(sent(fetchMock)).toMatchObject({ path: '/api/customers', method: 'GET' })
  })

  it('sends search, page and pageSize in the query string', async () => {
    const fetchMock = fakeFetch(200, { items: [], page: 2, pageSize: 10, totalCount: 0 })

    await listCustomers({ search: '+966 50', page: 2, pageSize: 10 })

    expect(sent(fetchMock).path).toBe('/api/customers?search=%2B966+50&page=2&pageSize=10')
  })

  it('creates a customer with POST /api/customers', async () => {
    const fetchMock = fakeFetch(201, { id: 'c1' })
    const request = { name: 'Nour Trading', email: 'info@nour.example', phone: null }

    await createCustomer(request)

    expect(sent(fetchMock)).toEqual({ path: '/api/customers', method: 'POST', body: request })
  })

  it('updates a customer with PUT /api/customers/{id}', async () => {
    const fetchMock = fakeFetch(200, { id: 'c1' })
    const request = { name: 'Nour Trading Co.', email: null, phone: '+966501234567' }

    await updateCustomer('c1', request)

    expect(sent(fetchMock)).toEqual({ path: '/api/customers/c1', method: 'PUT', body: request })
  })

  it('deletes a customer with DELETE /api/customers/{id} and no body', async () => {
    const fetchMock = fakeFetch(204)

    await deleteCustomer('c1')

    expect(sent(fetchMock)).toEqual({ path: '/api/customers/c1', method: 'DELETE', body: undefined })
  })

  it('reads one customer with its contacts with GET /api/customers/{id}', async () => {
    const fetchMock = fakeFetch(200, { id: 'c1', contacts: [] })

    await getCustomer('c1')

    expect(sent(fetchMock)).toEqual({ path: '/api/customers/c1', method: 'GET', body: undefined })
  })

  it('adds a contact with POST /api/customers/{id}/contacts', async () => {
    const fetchMock = fakeFetch(201, { id: 'k1' })
    const request = { type: 'whatsapp' as const, value: '0501234567', isPrimary: true }

    await addCustomerContact('c1', request)

    expect(sent(fetchMock)).toEqual({ path: '/api/customers/c1/contacts', method: 'POST', body: request })
  })

  it('makes a contact primary with POST …/contacts/{contactId}/primary and no body', async () => {
    const fetchMock = fakeFetch(204)

    await makeCustomerContactPrimary('c1', 'k1')

    expect(sent(fetchMock)).toEqual({ path: '/api/customers/c1/contacts/k1/primary', method: 'POST', body: undefined })
  })

  it('removes a contact with DELETE /api/customers/{id}/contacts/{contactId}', async () => {
    const fetchMock = fakeFetch(204)

    await removeCustomerContact('c1', 'k1')

    expect(sent(fetchMock)).toEqual({ path: '/api/customers/c1/contacts/k1', method: 'DELETE', body: undefined })
  })
})

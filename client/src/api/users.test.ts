import { afterEach, describe, expect, it, vi } from 'vitest'
import { createUser, deactivateUser, listUsers, reactivateUser, updateUser } from './users'

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

describe('users API', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lists users without a query string by default', async () => {
    const fetchMock = fakeFetch(200, { items: [], page: 1, pageSize: 20, totalCount: 0 })

    await listUsers({})

    expect(sent(fetchMock)).toMatchObject({ path: '/api/users', method: 'GET' })
  })

  it('sends search, page and pageSize in the query string', async () => {
    const fetchMock = fakeFetch(200, { items: [], page: 2, pageSize: 10, totalCount: 0 })

    await listUsers({ search: 'sara & co', page: 2, pageSize: 10 })

    expect(sent(fetchMock).path).toBe('/api/users?search=sara+%26+co&page=2&pageSize=10')
  })

  it('creates a user with POST /api/users', async () => {
    const fetchMock = fakeFetch(201, { id: 'u1' })
    const request = { email: 'a@crm.local', fullName: 'A', password: 'Agent#Pass1', roles: ['Agent' as const] }

    await createUser(request)

    expect(sent(fetchMock)).toEqual({ path: '/api/users', method: 'POST', body: request })
  })

  it('updates a user with PUT /api/users/{id}', async () => {
    const fetchMock = fakeFetch(200, { id: 'u1' })
    const request = { email: 'a@crm.local', fullName: 'A', roles: ['Admin' as const] }

    await updateUser('u1', request)

    expect(sent(fetchMock)).toEqual({ path: '/api/users/u1', method: 'PUT', body: request })
  })

  it('deactivates and reactivates with POST and no body', async () => {
    const deactivate = fakeFetch(204)
    await deactivateUser('u1')
    expect(sent(deactivate)).toEqual({ path: '/api/users/u1/deactivate', method: 'POST', body: undefined })

    const reactivate = fakeFetch(204)
    await reactivateUser('u1')
    expect(sent(reactivate)).toEqual({ path: '/api/users/u1/reactivate', method: 'POST', body: undefined })
  })
})

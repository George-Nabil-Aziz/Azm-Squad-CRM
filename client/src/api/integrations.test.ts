import { afterEach, describe, expect, it, vi } from 'vitest'
import { createApiKey, listApiKeys, revokeApiKey } from './integrations'

function fakeFetch(body: unknown = {}, status = 200) {
  const fetchMock = vi.fn().mockResolvedValue(
    status === 204
      ? new Response(null, { status })
      : new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('integrations API', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lists keys with GET /api/api-keys', async () => {
    const fetchMock = fakeFetch([])

    await listApiKeys()

    const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect([path, init.method]).toEqual(['/api/api-keys', 'GET'])
  })

  it('creates a key with POST /api/api-keys', async () => {
    const fetchMock = fakeFetch({ key: 'crm_x' })

    await createApiKey({ name: 'Shop', scopes: ['tickets:read'] })

    const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect([path, init.method, JSON.parse(String(init.body))]).toEqual([
      '/api/api-keys',
      'POST',
      { name: 'Shop', scopes: ['tickets:read'] },
    ])
  })

  it('revokes a key with DELETE /api/api-keys/{id}', async () => {
    const fetchMock = fakeFetch(null, 204)

    await revokeApiKey('k 1')

    const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect([path, init.method]).toEqual(['/api/api-keys/k%201', 'DELETE'])
  })
})

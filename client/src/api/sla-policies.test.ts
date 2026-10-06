import { afterEach, describe, expect, it, vi } from 'vitest'
import { listSlaPolicies, updateSlaPolicy } from './sla-policies'

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

describe('SLA policies API', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lists the policies with GET /api/sla-policies', async () => {
    const fetchMock = fakeFetch(200, [])

    await listSlaPolicies()

    expect(sent(fetchMock)).toMatchObject({ path: '/api/sla-policies', method: 'GET' })
  })

  it('updates one priority with PUT /api/sla-policies/{priority}', async () => {
    const fetchMock = fakeFetch(200, { priority: 'high' })

    await updateSlaPolicy('high', { responseMinutes: 60, resolutionMinutes: 240 })

    expect(sent(fetchMock)).toEqual({
      path: '/api/sla-policies/high',
      method: 'PUT',
      body: { responseMinutes: 60, resolutionMinutes: 240 },
    })
  })
})

import { afterEach, describe, expect, it, vi } from 'vitest'
import { getDashboardOverview, getSystemOverview } from './dashboard'

describe('dashboard API', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('reads the overview with GET /api/dashboard/overview', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify({ openTickets: 4 }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    vi.stubGlobal('fetch', fetchMock)

    const overview = await getDashboardOverview()

    const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect({ path, method: init.method }).toEqual({ path: '/api/dashboard/overview', method: 'GET' })
    expect(overview.openTickets).toBe(4)
  })

  it('reads the system overview with GET /api/dashboard/system-overview', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify({ customers: 60 }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    vi.stubGlobal('fetch', fetchMock)

    const overview = await getSystemOverview()

    const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect({ path, method: init.method }).toEqual({ path: '/api/dashboard/system-overview', method: 'GET' })
    expect(overview.customers).toBe(60)
  })
})

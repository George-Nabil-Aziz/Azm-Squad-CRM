import { afterEach, describe, expect, it, vi } from 'vitest'
import { getSettings, updateSettings, type UpdateSettingsRequest } from './settings'

function fakeFetch(body: unknown = {}) {
  const fetchMock = vi.fn().mockResolvedValue(
    new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } }),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('settings API', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('reads with GET /api/settings', async () => {
    const fetchMock = fakeFetch()

    await getSettings()

    const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect([path, init.method]).toEqual(['/api/settings', 'GET'])
  })

  it('saves with PUT /api/settings', async () => {
    const fetchMock = fakeFetch()
    const request = { ticketPrefix: 'SUP-', secrets: { smtpPassword: '' } } as UpdateSettingsRequest

    await updateSettings(request)

    const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect([path, init.method, JSON.parse(String(init.body))]).toEqual(['/api/settings', 'PUT', request])
  })
})

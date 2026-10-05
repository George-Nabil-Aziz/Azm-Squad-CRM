import { afterEach, describe, expect, it, vi } from 'vitest'
import { getAccessToken, saveSession } from '../auth/session'
import { apiGet, apiPost } from './client'

const inOneHour = () => new Date(Date.now() + 60 * 60 * 1000).toISOString()

function okJson(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
}

function sentHeaders(fetchMock: ReturnType<typeof vi.fn>): Record<string, string> {
  return (fetchMock.mock.calls[0][1] as RequestInit).headers as Record<string, string>
}

describe('API client authentication', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('sends the bearer token when signed in', async () => {
    saveSession('abc.def.ghi', inOneHour())
    const fetchMock = vi.fn().mockResolvedValue(okJson({}))
    vi.stubGlobal('fetch', fetchMock)

    await apiGet('/api/auth/me')

    expect(sentHeaders(fetchMock).Authorization).toBe('Bearer abc.def.ghi')
  })

  it('sends no Authorization header when signed out', async () => {
    const fetchMock = vi.fn().mockResolvedValue(okJson({}))
    vi.stubGlobal('fetch', fetchMock)

    await apiGet('/api/health')

    expect(sentHeaders(fetchMock)).not.toHaveProperty('Authorization')
  })

  it('signs out when the API rejects the token with 401', async () => {
    saveSession('expired-on-server', inOneHour())
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 401 })))

    await expect(apiGet('/api/auth/me')).rejects.toThrow('401')

    expect(getAccessToken()).toBeNull()
  })

  it('posts a JSON body', async () => {
    const fetchMock = vi.fn().mockResolvedValue(okJson({ ok: true }))
    vi.stubGlobal('fetch', fetchMock)

    await expect(apiPost('/api/auth/login', { email: 'a@b.c' })).resolves.toEqual({ ok: true })

    const init = fetchMock.mock.calls[0][1] as RequestInit
    expect(init.method).toBe('POST')
    expect(init.body).toBe('{"email":"a@b.c"}')
    expect(sentHeaders(fetchMock)['Content-Type']).toBe('application/json')
  })
})

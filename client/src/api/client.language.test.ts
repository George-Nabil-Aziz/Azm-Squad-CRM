import { afterEach, describe, expect, it, vi } from 'vitest'
import { setLanguage } from '../i18n/i18n'
import { apiGet, apiPost } from './client'

function okJson(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
}

function sentHeaders(fetchMock: ReturnType<typeof vi.fn>, call = 0): Record<string, string> {
  return (fetchMock.mock.calls[call][1] as RequestInit).headers as Record<string, string>
}

describe('API client language', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('sends Accept-Language "en" by default', async () => {
    const fetchMock = vi.fn(async (_path: string, _init?: RequestInit) => okJson({}))
    vi.stubGlobal('fetch', fetchMock)

    await apiGet('/api/health')

    expect(sentHeaders(fetchMock)['Accept-Language']).toBe('en')
  })

  it('sends Accept-Language of the language the user switched to', async () => {
    const fetchMock = vi.fn(async (_path: string, _init?: RequestInit) => okJson({}))
    vi.stubGlobal('fetch', fetchMock)

    await setLanguage('ar')
    await apiPost('/api/auth/login', { email: '', password: '' })
    await setLanguage('en')
    await apiGet('/api/health')

    expect(sentHeaders(fetchMock, 0)['Accept-Language']).toBe('ar')
    expect(sentHeaders(fetchMock, 1)['Accept-Language']).toBe('en')
  })
})

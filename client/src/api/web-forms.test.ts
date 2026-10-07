import { afterEach, describe, expect, it, vi } from 'vitest'
import { getWebFormConfig, submitWebForm } from './web-forms'

function fakeFetch(body: unknown = {}, status = 200) {
  const fetchMock = vi.fn().mockResolvedValue(
    new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('web forms API', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('reads the captcha config with GET /api/public/web-forms/config', async () => {
    const fetchMock = fakeFetch({ captchaRequired: true, captchaSiteKey: 'site' })

    const config = await getWebFormConfig()

    const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect([path, init.method, config.captchaSiteKey]).toEqual(['/api/public/web-forms/config', 'GET', 'site'])
  })

  it('submits with POST /api/public/web-forms', async () => {
    const fetchMock = fakeFetch({ number: 'TKT-000001' }, 201)
    const input = { name: 'Nour', email: 'n@x.example', subject: 'Hi', message: 'Hello', captchaToken: 'tok', website: '' }

    const receipt = await submitWebForm(input)

    const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect([path, init.method, JSON.parse(String(init.body)), receipt.number]).toEqual([
      '/api/public/web-forms',
      'POST',
      input,
      'TKT-000001',
    ])
  })
})

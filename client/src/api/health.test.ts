import { afterEach, describe, expect, it, vi } from 'vitest'
import { getHealth } from './health'

describe('getHealth', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('calls /api/health and returns the parsed body', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ status: 'ok' }), { status: 200 }),
    )
    vi.stubGlobal('fetch', fetchMock)

    await expect(getHealth()).resolves.toEqual({ status: 'ok' })
    expect(fetchMock).toHaveBeenCalledWith('/api/health', expect.anything())
  })

  it('throws when the API responds with an error status', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 503 })))

    await expect(getHealth()).rejects.toThrow('503')
  })
})

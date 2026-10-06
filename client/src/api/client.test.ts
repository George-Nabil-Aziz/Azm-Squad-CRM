import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiGet, apiGetBlob, apiPostForm, onApiError } from './client'
import { ApiError } from './errors'

describe('apiGet errors', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('throws ApiError with the ProblemDetails body and correlation id', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            status: 400,
            title: 'One or more validation errors occurred.',
            errors: { name: ["'Name' must not be empty."] },
            correlationId: 'corr-1',
          }),
          { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    )

    const error = await apiGet('/api/x').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    const apiError = error as ApiError
    expect(apiError.status).toBe(400)
    expect(apiError.problem?.errors).toEqual({ name: ["'Name' must not be empty."] })
    expect(apiError.correlationId).toBe('corr-1')
  })

  it('throws ApiError without a body when the response is not JSON', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('Bad gateway', { status: 502 })))

    const error = (await apiGet('/api/x').catch((e: unknown) => e)) as ApiError

    expect(error.status).toBe(502)
    expect(error.problem).toBeUndefined()
  })

  it('notifies subscribers once per failure and stops after unsubscribe', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 500 })))
    const listener = vi.fn()
    const unsubscribe = onApiError(listener)

    await apiGet('/api/x').catch(() => {})
    unsubscribe()
    await apiGet('/api/x').catch(() => {})

    expect(listener).toHaveBeenCalledTimes(1)
    expect(listener.mock.calls[0][0]).toBeInstanceOf(ApiError)
  })
})

describe('form uploads and file downloads', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('posts FormData as is, without a JSON content type', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ id: 'a1' }), { status: 201, headers: { 'Content-Type': 'application/json' } }),
    )
    vi.stubGlobal('fetch', fetchMock)
    const form = new FormData()
    form.append('file', new File(['x'], 'report.pdf'))

    const result = await apiPostForm<{ id: string }>('/api/x', form)

    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(init.method).toBe('POST')
    expect(init.body).toBe(form)
    expect((init.headers as Record<string, string>)['Content-Type']).toBeUndefined()
    expect(result).toEqual({ id: 'a1' })
  })

  it('reads a download as a Blob', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('%PDF', { status: 200 })))

    const blob = await apiGetBlob('/api/x')

    expect(await blob.text()).toBe('%PDF')
  })
})

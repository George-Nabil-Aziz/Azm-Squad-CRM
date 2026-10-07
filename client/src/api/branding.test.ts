import { afterEach, describe, expect, it, vi } from 'vitest'
import { onApiError } from './client'
import { getBranding, removeBrandingLogo, updateBranding, uploadBrandingLogo } from './branding'

function fakeFetch(status = 200, body: unknown = {}) {
  const fetchMock = vi.fn().mockResolvedValue(
    new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('branding API', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('reads the public branding with GET /api/branding', async () => {
    const fetchMock = fakeFetch(200, { primaryColor: '#0a5cad', secondaryColor: null, logoUrl: null })

    const branding = await getBranding()

    expect(branding.primaryColor).toBe('#0a5cad')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/branding')
    expect((fetchMock.mock.calls[0][1] as RequestInit).method).toBe('GET')
  })

  it('does not report a failed read to the error listeners (no toast)', async () => {
    fakeFetch(500, { status: 500 })
    const listener = vi.fn()
    const off = onApiError(listener)

    await expect(getBranding()).rejects.toThrow()

    off()
    expect(listener).not.toHaveBeenCalled()
  })

  it('saves the colours with PUT /api/branding', async () => {
    const fetchMock = fakeFetch()

    await updateBranding({ primaryColor: '#fff', secondaryColor: '' })

    const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(path).toBe('/api/branding')
    expect(init.method).toBe('PUT')
    expect(JSON.parse(String(init.body))).toEqual({ primaryColor: '#fff', secondaryColor: '' })
  })

  it('uploads the logo as multipart field "file" with PUT, and removes it with DELETE', async () => {
    const upload = fakeFetch()
    await uploadBrandingLogo(new File(['x'], 'logo.png', { type: 'image/png' }))
    const [path, init] = upload.mock.calls[0] as [string, RequestInit]
    expect(path).toBe('/api/branding/logo')
    expect(init.method).toBe('PUT')
    expect((init.body as FormData).get('file')).toBeInstanceOf(File)

    const remove = fakeFetch()
    await removeBrandingLogo()
    expect((remove.mock.calls[0][1] as RequestInit).method).toBe('DELETE')
  })
})

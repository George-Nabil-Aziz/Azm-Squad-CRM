import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiGet } from '../api/client'
import { ApiErrorToaster } from './ApiErrorToaster'

function problemResponse(status: number, body: object) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

describe('ApiErrorToaster', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows an error toast with the reference id when an API call fails', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        problemResponse(500, { status: 500, title: 'An unexpected error occurred.', correlationId: 'abc123' }),
      ),
    )
    render(<ApiErrorToaster />)

    await expect(apiGet('/api/anything')).rejects.toThrow('500')

    expect(await screen.findByText('Something went wrong. Please try again.')).toBeInTheDocument()
    expect(screen.getByText('Reference: abc123')).toBeInTheDocument()
  })

  it('shows a connection message when the server cannot be reached', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))
    render(<ApiErrorToaster />)

    await expect(apiGet('/api/anything')).rejects.toThrow('network error')

    expect(
      await screen.findByText('Cannot reach the server. Check your connection and try again.'),
    ).toBeInTheDocument()
  })

  it('does not show a toast when the request is aborted on purpose', async () => {
    const controller = new AbortController()
    vi.stubGlobal(
      'fetch',
      vi.fn().mockImplementation(() => {
        controller.abort()
        return Promise.reject(new DOMException('Aborted', 'AbortError'))
      }),
    )
    render(<ApiErrorToaster />)

    await expect(apiGet('/api/anything', controller.signal)).rejects.toThrow('Aborted')

    expect(screen.queryByRole('listitem')).not.toBeInTheDocument()
  })
})

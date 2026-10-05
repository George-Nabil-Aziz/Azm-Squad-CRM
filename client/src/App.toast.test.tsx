import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'

// No module mocks here: the real API client runs against a stubbed fetch,
// so this proves the whole path API failure → toast on the home page.
describe('App error toast', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows an error toast when the health call fails', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ status: 500, correlationId: 'home-1' }), {
          status: 500,
          headers: { 'Content-Type': 'application/problem+json' },
        }),
      ),
    )

    render(<App />)

    expect(await screen.findByText('unavailable')).toBeInTheDocument()
    expect(await screen.findByText('Something went wrong. Please try again.')).toBeInTheDocument()
    expect(screen.getByText('Reference: home-1')).toBeInTheDocument()
  })
})

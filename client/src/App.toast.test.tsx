import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { saveSession } from './auth/session'
import { fakeApi, inOneHour } from './test/fake-api'

// No module mocks here: the real API client runs against a stubbed fetch,
// so this proves the whole path API failure → toast on the dashboard.
describe('App error toast', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows an error toast when an API call fails', async () => {
    saveSession('good-token', inOneHour())
    vi.stubGlobal('fetch', fakeApi({ healthStatus: 500 }))

    render(<App />)

    expect(await screen.findByText('Something went wrong. Please try again.')).toBeInTheDocument()
    expect(screen.getByText('Reference: health-1')).toBeInTheDocument()
  })
})

import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { getHealth } from './api/health'

vi.mock('./api/health', () => ({ getHealth: vi.fn() }))

describe('App', () => {
  beforeEach(() => {
    vi.mocked(getHealth).mockReset()
  })

  it('shows "ok" when the API is healthy', async () => {
    vi.mocked(getHealth).mockResolvedValue({ status: 'ok' })
    render(<App />)
    expect(await screen.findByText('ok')).toBeInTheDocument()
  })

  it('shows "unavailable" when the API call fails', async () => {
    vi.mocked(getHealth).mockRejectedValue(new Error('503'))
    render(<App />)
    expect(await screen.findByText('unavailable')).toBeInTheDocument()
  })
})

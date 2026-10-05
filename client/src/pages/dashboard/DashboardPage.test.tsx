import { QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser } from '@/api/auth'
import { getHealth } from '@/api/health'
import { createQueryClient } from '@/app/query-client'
import { DashboardPage } from './DashboardPage'

vi.mock('@/api/health', () => ({ getHealth: vi.fn() }))
vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))

function renderDashboard() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <DashboardPage />
    </QueryClientProvider>,
  )
}

describe('DashboardPage', () => {
  beforeEach(() => {
    vi.mocked(getHealth).mockReset()
    vi.mocked(getCurrentUser).mockResolvedValue({
      id: '1',
      email: 'admin@crm.local',
      fullName: 'System Administrator',
      roles: ['SuperAdmin'],
    })
  })

  it('welcomes the signed-in user', async () => {
    vi.mocked(getHealth).mockResolvedValue({ status: 'ok' })
    renderDashboard()

    expect(screen.getByRole('heading', { level: 1, name: 'Dashboard' })).toBeInTheDocument()
    expect(await screen.findByText('Welcome, System Administrator')).toBeInTheDocument()
  })

  it('shows "ok" when the API is healthy', async () => {
    vi.mocked(getHealth).mockResolvedValue({ status: 'ok' })
    renderDashboard()

    expect(await screen.findByText('ok')).toBeInTheDocument()
  })

  it('shows "unavailable" when the API call fails', async () => {
    vi.mocked(getHealth).mockRejectedValue(new Error('503'))
    renderDashboard()

    expect(await screen.findByText('unavailable')).toBeInTheDocument()
  })
})

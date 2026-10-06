import { QueryClientProvider } from '@tanstack/react-query'
import { render, renderHook, screen, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getDashboard, type Dashboard } from '@/api/reports'
import { createQueryClient } from '@/app/query-client'
import { DASHBOARD_REFRESH_MS, useDashboard } from '@/features/reports/useDashboard'
import { DashboardReportPage } from './DashboardReportPage'

vi.mock('@/api/reports', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/reports')>()),
  getDashboard: vi.fn(),
}))

const dashboard: Dashboard = {
  generatedAt: '2026-10-06T12:00:00Z',
  openTickets: 12,
  breachedToday: 3,
  averageResponseMinutes: 90,
  averageCsat: 4.5,
  csatCount: 8,
  ticketsPerDay: [
    { date: '2026-10-05', count: 4 },
    { date: '2026-10-06', count: 6 },
  ],
  ticketsByChannel: [
    { key: 'manual', count: 1 },
    { key: 'email', count: 9 },
    { key: 'whatsapp', count: 0 },
    { key: 'portal', count: 0 },
  ],
}

function wrapper({ children }: { children: ReactNode }) {
  return <QueryClientProvider client={createQueryClient()}>{children}</QueryClientProvider>
}

describe('DashboardReportPage', () => {
  beforeEach(() => {
    vi.mocked(getDashboard).mockReset().mockResolvedValue(dashboard)
  })

  it('shows the four KPI cards', async () => {
    render(<DashboardReportPage />, { wrapper })

    expect(await screen.findByText('12')).toBeInTheDocument()
    expect(screen.getByText('Open tickets')).toBeInTheDocument()
    expect(screen.getByText('Breached today')).toBeInTheDocument()
    expect(screen.getByText('3')).toBeInTheDocument()
    expect(screen.getByText('1 h 30 min')).toBeInTheDocument()
    expect(screen.getByText('4.5')).toBeInTheDocument()
    expect(screen.getByText('8 ratings')).toBeInTheDocument()
  })

  it('shows the two charts and when the data was updated', async () => {
    render(<DashboardReportPage />, { wrapper })

    expect(await screen.findByRole('img', { name: 'Tickets per day' })).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'Tickets by channel' })).toBeInTheDocument()
    expect(screen.getByText(/^Updated at /)).toBeInTheDocument()
  })

  it('shows dashes while there is no response time or rating yet', async () => {
    vi.mocked(getDashboard).mockResolvedValue({ ...dashboard, averageResponseMinutes: null, averageCsat: null, csatCount: 0 })
    render(<DashboardReportPage />, { wrapper })

    await screen.findByText('12')
    expect(screen.getAllByText('–')).toHaveLength(2)
  })
})

describe('useDashboard', () => {
  beforeEach(() => {
    vi.mocked(getDashboard).mockReset().mockResolvedValue(dashboard)
  })

  it('reloads itself on an interval (real time)', async () => {
    renderHook(() => useDashboard(30), { wrapper })

    await waitFor(() => expect(vi.mocked(getDashboard).mock.calls.length).toBeGreaterThanOrEqual(3))
  })

  it('refreshes every 30 seconds by default', () => {
    expect(DASHBOARD_REFRESH_MS).toBe(30_000)
  })
})

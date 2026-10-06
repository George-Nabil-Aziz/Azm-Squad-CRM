import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getSlaReport, listSlaBreaches, type SlaPriorityRow, type SlaReport } from '@/api/reports'
import { createQueryClient } from '@/app/query-client'
import { SlaReportPage } from './SlaReportPage'

vi.mock('@/api/reports', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/reports')>()),
  getSlaReport: vi.fn(),
  listSlaBreaches: vi.fn(),
}))

const row = (priority: string, tickets: number, response: [number, number, number, number | null, number | null], resolution: [number, number, number, number | null, number | null]): SlaPriorityRow => ({
  priority: priority as SlaPriorityRow['priority'],
  tickets,
  response: { met: response[0], breached: response[1], pending: response[2], compliancePercent: response[3], averageMinutes: response[4] },
  resolution: { met: resolution[0], breached: resolution[1], pending: resolution[2], compliancePercent: resolution[3], averageMinutes: resolution[4] },
})

const report: SlaReport = {
  from: '2026-10-01',
  to: '2026-10-05',
  overall: row('all', 5, [2, 1, 1, 66.7, 82.5], [1, 1, 3, 50, 420]),
  priorities: [
    row('high', 3, [1, 1, 1, 50, 120], [1, 1, 1, 50, 420]),
    row('mid', 1, [1, 0, 0, 100, 60], [0, 0, 1, null, null]),
    row('low', 1, [0, 0, 0, null, 30], [0, 0, 1, null, null]),
  ],
}

const breaches = {
  items: [
    {
      ticketId: 't-9',
      number: 'TKT-000009',
      subject: 'Printer is down',
      priority: 'high' as const,
      assigneeName: 'Sara Agent',
      createdAt: '2026-10-02T08:00:00Z',
      responseDueAt: '2026-10-02T10:00:00Z',
      firstResponseAt: null,
      resolutionDueAt: '2026-10-02T16:00:00Z',
      resolvedAt: null,
      responseBreached: true,
      resolutionBreached: false,
    },
  ],
  page: 1,
  pageSize: 20,
  totalCount: 45,
}

function renderPage() {
  return render(
    <MemoryRouter>
      <QueryClientProvider client={createQueryClient()}>
        <SlaReportPage />
      </QueryClientProvider>
    </MemoryRouter>,
  )
}

describe('SlaReportPage', () => {
  beforeEach(() => {
    vi.mocked(getSlaReport).mockReset().mockResolvedValue(report)
    vi.mocked(listSlaBreaches).mockReset().mockResolvedValue(breaches)
  })

  it('shows compliance and averages per priority and overall', async () => {
    renderPage()

    const high = await screen.findByRole('row', { name: /^High/ })
    expect(within(high).getAllByText('50%')).toHaveLength(2)
    expect(within(high).getByText('2 h')).toBeInTheDocument() // average first response 120 min
    expect(within(high).getByText('7 h')).toBeInTheDocument() // average resolution 420 min
    const mid = screen.getByRole('row', { name: /^Mid/ })
    expect(within(mid).getByText('100%')).toBeInTheDocument()
    expect(within(mid).getAllByText('–')).toHaveLength(2) // resolution: nothing decided yet
    const overall = screen.getByRole('row', { name: /^All priorities/ })
    expect(within(overall).getByText('66.7%')).toBeInTheDocument()
    expect(within(overall).getByText('1 h 23 min')).toBeInTheDocument() // 82.5 min, rounded
  })

  it('lists breached tickets that link to the ticket', async () => {
    renderPage()

    const link = await screen.findByRole('link', { name: 'TKT-000009' })
    expect(link).toHaveAttribute('href', '/tickets/t-9')
    const row = screen.getByRole('row', { name: /TKT-000009/ })
    expect(within(row).getByText('Printer is down')).toBeInTheDocument()
    expect(within(row).getByText('Sara Agent')).toBeInTheDocument()
    expect(within(row).getByText('Response')).toBeInTheDocument() // which target was missed
  })

  it('sends the date range to both calls', async () => {
    renderPage()
    await screen.findByRole('row', { name: /^High/ })

    fireEvent.change(screen.getByLabelText('From'), { target: { value: '2026-09-01' } })
    fireEvent.change(screen.getByLabelText('To'), { target: { value: '2026-09-30' } })

    await waitFor(() => expect(getSlaReport).toHaveBeenLastCalledWith({ from: '2026-09-01', to: '2026-09-30' }, expect.anything()))
    await waitFor(() =>
      expect(listSlaBreaches).toHaveBeenLastCalledWith(
        expect.objectContaining({ from: '2026-09-01', to: '2026-09-30', page: 1 }),
        expect.anything(),
      ),
    )
  })

  it('pages through the breached tickets', async () => {
    renderPage()
    await screen.findByText('Page 1 of 3')

    fireEvent.click(screen.getByRole('button', { name: 'Next' }))

    await waitFor(() => expect(listSlaBreaches).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2 }), expect.anything()))
  })

  it('says so when no ticket breached', async () => {
    vi.mocked(listSlaBreaches).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    renderPage()

    expect(await screen.findByText('No breached tickets in this period.')).toBeInTheDocument()
  })
})

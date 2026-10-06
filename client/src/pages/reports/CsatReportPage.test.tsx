import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCsatReport, type CsatReport } from '@/api/reports'
import { createQueryClient } from '@/app/query-client'
import { CsatReportPage } from './CsatReportPage'

vi.mock('@/api/reports', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/reports')>()),
  getCsatReport: vi.fn(),
}))

const report: CsatReport = {
  from: '2026-10-01',
  to: '2026-10-02',
  totalRatings: 4,
  averageRating: 3.5,
  distribution: [
    { rating: 1, count: 1 },
    { rating: 2, count: 0 },
    { rating: 3, count: 0 },
    { rating: 4, count: 2 },
    { rating: 5, count: 1 },
  ],
  byDay: [
    { date: '2026-10-01', averageRating: 4.5, count: 2 },
    { date: '2026-10-02', averageRating: null, count: 0 },
  ],
  byAgent: [{ id: 'a-1', name: 'Sara Agent', averageRating: 4, count: 3 }],
  byCategory: [{ id: null, name: null, averageRating: 2, count: 1 }],
  lowRatings: [
    { ticketId: 't-1', ticketNumber: 'TKT-000001', rating: 1, comment: 'Never solved', ratedAt: '2026-10-01T10:00:00Z', agentName: 'Sara Agent' },
  ],
  surveysSent: 10,
  responseRatePercent: 40,
}

function renderPage() {
  return render(
    <MemoryRouter>
      <QueryClientProvider client={createQueryClient()}>
        <CsatReportPage />
      </QueryClientProvider>
    </MemoryRouter>,
  )
}

describe('CsatReportPage', () => {
  beforeEach(() => {
    vi.mocked(getCsatReport).mockReset().mockResolvedValue(report)
  })

  it('shows the average, the response rate and the 1-5 distribution', async () => {
    renderPage()

    expect(await screen.findByText('3.5')).toBeInTheDocument()
    expect(screen.getByText('40%')).toBeInTheDocument()
    expect(screen.getByText('4 of 10 surveys answered')).toBeInTheDocument()
    const distribution = screen.getByRole('table', { name: 'Rating distribution' })
    expect(within(within(distribution).getByRole('row', { name: /^4 stars/ })).getByText('2')).toBeInTheDocument()
  })

  it('shows the trend and the groupings by agent and category', async () => {
    renderPage()

    const trend = await screen.findByRole('table', { name: 'Average rating by day' })
    expect(within(within(trend).getByRole('row', { name: /2026-10-01/ })).getByText('4.5')).toBeInTheDocument()
    expect(within(within(trend).getByRole('row', { name: /2026-10-02/ })).getByText('–')).toBeInTheDocument()
    expect(within(screen.getByRole('table', { name: 'By agent' })).getByRole('row', { name: /Sara Agent/ })).toBeInTheDocument()
    expect(within(screen.getByRole('table', { name: 'By category' })).getByRole('row', { name: /Uncategorized/ })).toBeInTheDocument()
  })

  it('lists low ratings with their comments, linked to the ticket', async () => {
    renderPage()

    const link = await screen.findByRole('link', { name: 'TKT-000001' })
    expect(link).toHaveAttribute('href', '/tickets/t-1')
    expect(screen.getByText('Never solved')).toBeInTheDocument()
  })

  it('asks for the chosen date range', async () => {
    renderPage()
    await screen.findByText('3.5')

    fireEvent.change(screen.getByLabelText('From'), { target: { value: '2026-09-01' } })
    fireEvent.change(screen.getByLabelText('To'), { target: { value: '2026-09-30' } })

    await waitFor(() => expect(getCsatReport).toHaveBeenLastCalledWith({ from: '2026-09-01', to: '2026-09-30' }, expect.anything()))
  })

  it('shows dashes when nothing was rated or sent', async () => {
    vi.mocked(getCsatReport).mockResolvedValue({
      ...report,
      totalRatings: 0,
      averageRating: null,
      lowRatings: [],
      surveysSent: 0,
      responseRatePercent: null,
      byAgent: [],
      byCategory: [],
    })
    renderPage()

    expect(await screen.findByText('No low ratings in this period.')).toBeInTheDocument()
    expect(screen.getByText('0 of 0 surveys answered')).toBeInTheDocument()
  })
})

import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { exportTicketReport, getTicketReport, type TicketReport } from '@/api/reports'
import { listTicketCategories } from '@/api/ticket-categories'
import { createQueryClient } from '@/app/query-client'
import { saveFile } from '@/lib/save-file'
import { TicketReportPage } from './TicketReportPage'

vi.mock('@/api/reports', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/reports')>()),
  getTicketReport: vi.fn(),
  exportTicketReport: vi.fn(),
}))
vi.mock('@/api/ticket-categories', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/ticket-categories')>()),
  listTicketCategories: vi.fn(),
}))
vi.mock('@/lib/save-file', () => ({ saveFile: vi.fn() }))

const report: TicketReport = {
  from: '2026-10-01',
  to: '2026-10-03',
  total: 7,
  byStatus: [
    { key: 'new', count: 1 },
    { key: 'open', count: 4 },
    { key: 'pending', count: 0 },
    { key: 'resolved', count: 2 },
    { key: 'closed', count: 0 },
  ],
  byCategory: [
    { categoryId: 'c-1', name: 'Billing', count: 5 },
    { categoryId: null, name: null, count: 2 },
  ],
  byChannel: [
    { key: 'manual', count: 3 },
    { key: 'email', count: 4 },
    { key: 'whatsapp', count: 0 },
    { key: 'portal', count: 0 },
  ],
  byPriority: [
    { key: 'high', count: 2 },
    { key: 'mid', count: 4 },
    { key: 'low', count: 1 },
  ],
  byDay: [
    { date: '2026-10-01', count: 2 },
    { date: '2026-10-02', count: 5 },
    { date: '2026-10-03', count: 0 },
  ],
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <TicketReportPage />
    </QueryClientProvider>,
  )
}

function table(name: string) {
  return screen.getByRole('table', { name })
}

describe('TicketReportPage', () => {
  beforeEach(() => {
    vi.mocked(getTicketReport).mockReset().mockResolvedValue(report)
    vi.mocked(exportTicketReport).mockReset().mockResolvedValue(new Blob(['x']))
    vi.mocked(saveFile).mockReset()
    vi.mocked(listTicketCategories).mockReset().mockResolvedValue([
      { id: 'c-1', name: 'Billing', isActive: true, createdAt: '2026-10-01T00:00:00Z', updatedAt: '2026-10-01T00:00:00Z' },
    ])
  })

  it('shows the total and the counts of every breakdown', async () => {
    renderPage()

    expect(await screen.findByText('7')).toBeInTheDocument()
    expect(screen.getByText('Tickets created from 2026-10-01 to 2026-10-03')).toBeInTheDocument()
    expect(within(within(table('By status')).getByRole('row', { name: /Open/ })).getByText('4')).toBeInTheDocument()
    expect(within(within(table('By category')).getByRole('row', { name: /Billing/ })).getByText('5')).toBeInTheDocument()
    expect(within(within(table('By category')).getByRole('row', { name: /Uncategorized/ })).getByText('2')).toBeInTheDocument()
    expect(within(within(table('By channel')).getByRole('row', { name: /Email/ })).getByText('4')).toBeInTheDocument()
    expect(within(within(table('By priority')).getByRole('row', { name: /High/ })).getByText('2')).toBeInTheDocument()
    expect(within(within(table('By day')).getByRole('row', { name: /2026-10-02/ })).getByText('5')).toBeInTheDocument()
  })

  it('asks the API for the chosen date range and filters', async () => {
    renderPage()
    await screen.findByText('7')

    fireEvent.change(screen.getByLabelText('From'), { target: { value: '2026-09-01' } })
    fireEvent.change(screen.getByLabelText('To'), { target: { value: '2026-09-30' } })
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'open' } })
    fireEvent.change(screen.getByLabelText('Channel'), { target: { value: 'email' } })
    fireEvent.change(screen.getByLabelText('Priority'), { target: { value: 'high' } })
    fireEvent.change(await screen.findByLabelText('Category'), { target: { value: 'c-1' } })

    await waitFor(() =>
      expect(getTicketReport).toHaveBeenLastCalledWith(
        { from: '2026-09-01', to: '2026-09-30', status: 'open', channel: 'email', priority: 'high', categoryId: 'c-1' },
        expect.anything(),
      ),
    )
  })

  it('exports the current filters as CSV and as Excel', async () => {
    renderPage()
    await screen.findByText('7')
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'open' } })

    fireEvent.click(screen.getByRole('button', { name: 'Export CSV' }))
    await waitFor(() => expect(saveFile).toHaveBeenCalledTimes(1))
    expect(exportTicketReport).toHaveBeenLastCalledWith({ status: 'open' }, 'csv')
    expect(vi.mocked(saveFile).mock.calls[0][1]).toBe('ticket-report-2026-10-01_2026-10-03.csv')

    fireEvent.click(screen.getByRole('button', { name: 'Export Excel' }))
    await waitFor(() => expect(saveFile).toHaveBeenCalledTimes(2))
    expect(exportTicketReport).toHaveBeenLastCalledWith({ status: 'open' }, 'xlsx')
    expect(vi.mocked(saveFile).mock.calls[1][1]).toBe('ticket-report-2026-10-01_2026-10-03.xlsx')
  })

  it('clears the filters', async () => {
    renderPage()
    await screen.findByText('7')
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'open' } })

    fireEvent.click(screen.getByRole('button', { name: 'Clear filters' }))

    expect(screen.getByLabelText('Status')).toHaveValue('')
    expect(screen.getByLabelText('From')).toHaveValue('')
    expect(await screen.findByText('7')).toBeInTheDocument()
  })
})

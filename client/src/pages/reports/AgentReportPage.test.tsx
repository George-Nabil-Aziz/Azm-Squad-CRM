import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { exportAgentReport, getAgentReport, type AgentReport } from '@/api/reports'
import { createQueryClient } from '@/app/query-client'
import { saveFile } from '@/lib/save-file'
import { AgentReportPage } from './AgentReportPage'

vi.mock('@/api/reports', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/reports')>()),
  getAgentReport: vi.fn(),
  exportAgentReport: vi.fn(),
}))
vi.mock('@/lib/save-file', () => ({ saveFile: vi.fn() }))

const report: AgentReport = {
  from: '2026-10-01',
  to: '2026-10-05',
  agents: [
    {
      agentId: 'a-1',
      name: 'Sara Agent',
      ticketsHandled: 6,
      averageFirstResponseMinutes: 30,
      averageResolutionMinutes: 750,
      slaPercent: 62.5,
      averageCsat: 4.5,
      csatCount: 2,
    },
    {
      agentId: 'a-2',
      name: 'Omar Agent',
      ticketsHandled: 2,
      averageFirstResponseMinutes: null,
      averageResolutionMinutes: null,
      slaPercent: null,
      averageCsat: null,
      csatCount: 0,
    },
  ],
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <AgentReportPage />
    </QueryClientProvider>,
  )
}

describe('AgentReportPage', () => {
  beforeEach(() => {
    vi.mocked(getAgentReport).mockReset().mockResolvedValue(report)
    vi.mocked(exportAgentReport).mockReset().mockResolvedValue(new Blob(['x']))
    vi.mocked(saveFile).mockReset()
  })

  it('shows one row per agent with the performance numbers', async () => {
    renderPage()

    const sara = await screen.findByRole('row', { name: /Sara Agent/ })
    expect(within(sara).getByText('6')).toBeInTheDocument()
    expect(within(sara).getByText('30 min')).toBeInTheDocument()
    expect(within(sara).getByText('12 h 30 min')).toBeInTheDocument()
    expect(within(sara).getByText('62.5%')).toBeInTheDocument()
    expect(within(sara).getByText('4.5')).toBeInTheDocument()
    expect(within(screen.getByRole('row', { name: /Omar Agent/ })).getAllByText('–')).toHaveLength(4)
  })

  it('asks for the chosen date range', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Sara Agent/ })

    fireEvent.change(screen.getByLabelText('From'), { target: { value: '2026-09-01' } })
    fireEvent.change(screen.getByLabelText('To'), { target: { value: '2026-09-30' } })

    await waitFor(() => expect(getAgentReport).toHaveBeenLastCalledWith({ from: '2026-09-01', to: '2026-09-30' }, expect.anything()))
  })

  it('exports the report as CSV and Excel', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Sara Agent/ })

    fireEvent.click(screen.getByRole('button', { name: 'Export CSV' }))
    await waitFor(() => expect(saveFile).toHaveBeenCalledTimes(1))
    expect(exportAgentReport).toHaveBeenLastCalledWith({}, 'csv')
    expect(vi.mocked(saveFile).mock.calls[0][1]).toBe('agent-report-2026-10-01_2026-10-05.csv')

    fireEvent.click(screen.getByRole('button', { name: 'Export Excel' }))
    await waitFor(() => expect(saveFile).toHaveBeenCalledTimes(2))
    expect(exportAgentReport).toHaveBeenLastCalledWith({}, 'xlsx')
  })

  it('says so when no agent handled tickets', async () => {
    vi.mocked(getAgentReport).mockResolvedValue({ ...report, agents: [] })
    renderPage()

    expect(await screen.findByText('No agent handled tickets in this period.')).toBeInTheDocument()
  })
})

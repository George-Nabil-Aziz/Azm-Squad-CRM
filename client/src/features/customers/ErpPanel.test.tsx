import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser } from '@/api/auth'
import { getCustomerErp, linkCustomerToErp, listErpSyncLogs, type ErpCustomerData } from '@/api/integrations'
import { createQueryClient } from '@/app/query-client'
import { saveSession } from '@/auth/session'
import { ErpLogsPage } from '@/pages/integrations/ErpLogsPage'
import { agentMe, inOneHour } from '@/test/fake-api'
import { ErpPanel } from './ErpPanel'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))
vi.mock('@/api/integrations', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/integrations')>()),
  getCustomerErp: vi.fn(),
  linkCustomerToErp: vi.fn(),
  listErpSyncLogs: vi.fn(),
}))

const linked: ErpCustomerData = {
  linked: true,
  erpCustomerId: 'ERP-42',
  available: true,
  message: null,
  orders: [{ id: 'o1', number: 'SO-1001', date: '2026-09-01T00:00:00Z', status: 'shipped', total: 120, currency: 'SAR' }],
  invoices: [{ id: 'i1', number: 'INV-77', date: '2026-09-02T00:00:00Z', dueDate: '2026-10-02T00:00:00Z', status: 'paid', total: 120, currency: 'SAR' }],
  fetchedAt: '2026-10-07T08:00:00Z',
}

function renderPanel() {
  saveSession('good-token', inOneHour())
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <ErpPanel customerId="c1" />
    </QueryClientProvider>,
  )
}

describe('ErpPanel', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(agentMe)
    vi.mocked(getCustomerErp).mockReset().mockResolvedValue(linked)
    vi.mocked(linkCustomerToErp).mockReset().mockResolvedValue({ customerId: 'c1', erpCustomerId: 'ERP-42' })
  })

  it('shows the recent orders and invoices read only', async () => {
    renderPanel()

    expect(await screen.findByText('SO-1001')).toBeInTheDocument()
    expect(screen.getByText('INV-77')).toBeInTheDocument()
    expect(screen.getByText('shipped')).toBeInTheDocument()
    expect(screen.getByText('Linked to ERP customer ERP-42')).toBeInTheDocument()
  })

  it('shows a clear message when the ERP is down and the rest of the page still renders', async () => {
    vi.mocked(getCustomerErp).mockResolvedValue({ ...linked, available: false, message: 'The ERP system is not reachable right now.', orders: [], invoices: [] })
    renderPanel()

    expect(await screen.findByRole('alert')).toHaveTextContent('The ERP system is not reachable right now.')
    expect(screen.queryByText('SO-1001')).not.toBeInTheDocument()
  })

  it('links an unlinked customer by ERP id', async () => {
    vi.mocked(getCustomerErp).mockResolvedValue({ ...linked, linked: false, erpCustomerId: null, orders: [], invoices: [] })
    renderPanel()

    fireEvent.change(await screen.findByLabelText('ERP customer id'), { target: { value: 'ERP-42' } })
    fireEvent.click(screen.getByRole('button', { name: 'Link' }))

    await waitFor(() => expect(linkCustomerToErp).toHaveBeenCalledWith('c1', 'ERP-42'))
  })
})

describe('ErpLogsPage', () => {
  it('lists every sync with result and time', async () => {
    vi.mocked(listErpSyncLogs).mockResolvedValue({
      items: [
        { id: 'l1', customerId: 'c1', customerName: 'Nour Trading', erpCustomerId: 'ERP-42', result: 'failed', error: 'The ERP answered 503', createdAt: '2026-10-07T08:00:00Z' },
      ],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    })
    render(
      <QueryClientProvider client={createQueryClient()}>
        <ErpLogsPage />
      </QueryClientProvider>,
    )

    expect(await screen.findByText('Nour Trading')).toBeInTheDocument()
    expect(screen.getByText('Failed')).toBeInTheDocument()
    expect(screen.getByText('The ERP answered 503')).toBeInTheDocument()
  })
})

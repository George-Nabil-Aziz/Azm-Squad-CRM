import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { listBranches, type Branch } from '@/api/branches'
import { getTicketReport } from '@/api/reports'
import { createQueryClient } from '@/app/query-client'
import { ReportsLayout } from '@/features/reports/ReportsLayout'
import { useTicketReport } from '@/features/reports/useTicketReport'

vi.mock('@/api/branches', () => ({ listBranches: vi.fn() }))
vi.mock('@/api/reports', () => ({ getTicketReport: vi.fn() }))

const branch = (id: string, name: string): Branch => ({
  id,
  name,
  isActive: true,
  createdAt: '2026-10-01T08:00:00Z',
  updatedAt: '2026-10-01T08:00:00Z',
})

function Probe() {
  useTicketReport({})
  return null
}

describe('report branch filter', () => {
  beforeEach(() => {
    vi.mocked(listBranches).mockReset().mockResolvedValue([branch('b1', 'Riyadh'), branch('b2', 'Jeddah')])
    vi.mocked(getTicketReport).mockReset().mockResolvedValue({} as never)
  })

  it('lets the user pick a branch, which is sent with the report request', async () => {
    const { MemoryRouter, Route, Routes } = await import('react-router')
    render(
      <QueryClientProvider client={createQueryClient()}>
        <MemoryRouter>
          <Routes>
            <Route element={<ReportsLayout />}>
              <Route index element={<Probe />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    )

    await screen.findByRole('option', { name: 'Riyadh' })
    expect(getTicketReport).toHaveBeenCalledWith({}, expect.anything())
    fireEvent.change(screen.getByRole('combobox', { name: 'Branch' }), { target: { value: 'b2' } })

    await vi.waitFor(() => expect(getTicketReport).toHaveBeenCalledWith({ branchId: 'b2' }, expect.anything()))
  })

  it('shows no branch filter when there are no branches', async () => {
    vi.mocked(listBranches).mockResolvedValue([])
    const { MemoryRouter, Route, Routes } = await import('react-router')
    render(
      <QueryClientProvider client={createQueryClient()}>
        <MemoryRouter>
          <Routes>
            <Route element={<ReportsLayout />}>
              <Route index element={<Probe />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    )

    await vi.waitFor(() => expect(listBranches).toHaveBeenCalled())
    expect(screen.queryByRole('combobox', { name: 'Branch' })).not.toBeInTheDocument()
  })
})

import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { listAuditLogs, type AuditLogEntry } from '@/api/audit-logs'
import { listUsers } from '@/api/users'
import { createQueryClient } from '@/app/query-client'
import { AuditLogsPage } from './AuditLogsPage'

vi.mock('@/api/audit-logs', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/audit-logs')>()),
  listAuditLogs: vi.fn(),
}))
vi.mock('@/api/users', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/users')>()),
  listUsers: vi.fn(),
}))

const entries: AuditLogEntry[] = [
  {
    id: 2,
    occurredAt: '2026-10-06T09:30:00Z',
    userId: null,
    userEmail: 'ghost@crm.local',
    action: 'login.failed',
    entityType: 'User',
    entityId: null,
    oldValues: null,
    newValues: '{"reason":"unknown-user"}',
    ipAddress: '10.0.0.8',
  },
  {
    id: 1,
    occurredAt: '2026-10-06T09:00:00Z',
    userId: 'u-1',
    userEmail: 'admin@crm.local',
    action: 'sla-policy.updated',
    entityType: 'SlaPolicy',
    entityId: 'high',
    oldValues: '{"responseMinutes":120}',
    newValues: '{"responseMinutes":60}',
    ipAddress: '10.0.0.9',
  },
]

function page(items: AuditLogEntry[], totalCount = items.length) {
  return { items, page: 1, pageSize: 20, totalCount }
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <AuditLogsPage />
    </QueryClientProvider>,
  )
}

describe('AuditLogsPage', () => {
  beforeEach(() => {
    vi.mocked(listAuditLogs).mockReset().mockResolvedValue(page(entries))
    vi.mocked(listUsers)
      .mockReset()
      .mockResolvedValue({
        items: [{ id: 'u-1', email: 'admin@crm.local', fullName: 'System Administrator', roles: ['SuperAdmin'], isActive: true }],
        page: 1,
        pageSize: 100,
        totalCount: 1,
      })
  })

  it('lists who did what, on which entity, with old/new values and IP', async () => {
    renderPage()

    expect(screen.getByRole('heading', { level: 1, name: 'Audit log' })).toBeInTheDocument()
    const failed = await screen.findByRole('row', { name: /ghost@crm.local/ })
    expect(within(failed).getByText('Login failed')).toBeInTheDocument()
    expect(within(failed).getByText('10.0.0.8')).toBeInTheDocument()
    expect(within(failed).getByText('{"reason":"unknown-user"}')).toBeInTheDocument()
    const sla = screen.getByRole('row', { name: /admin@crm.local/ })
    expect(within(sla).getByText('SLA policy changed')).toBeInTheDocument()
    expect(within(sla).getByText('{"responseMinutes":120}')).toBeInTheDocument()
    expect(within(sla).getByText('{"responseMinutes":60}')).toBeInTheDocument()
  })

  it('filters by action and user through the API', async () => {
    renderPage()
    await screen.findByRole('row', { name: /ghost@crm.local/ })

    fireEvent.change(screen.getByLabelText('Action'), { target: { value: 'login.failed' } })
    await waitFor(() => expect(listAuditLogs).toHaveBeenLastCalledWith(expect.objectContaining({ action: 'login.failed' }), expect.anything()))

    fireEvent.change(await screen.findByLabelText('User'), { target: { value: 'u-1' } })
    await waitFor(() =>
      expect(listAuditLogs).toHaveBeenLastCalledWith(expect.objectContaining({ action: 'login.failed', userId: 'u-1' }), expect.anything()),
    )
  })

  it('filters by a date range sent as UTC instants', async () => {
    renderPage()
    await screen.findByRole('row', { name: /ghost@crm.local/ })

    fireEvent.change(screen.getByLabelText('From'), { target: { value: '2026-10-01' } })
    fireEvent.change(screen.getByLabelText('To'), { target: { value: '2026-10-02' } })

    await waitFor(() =>
      expect(listAuditLogs).toHaveBeenLastCalledWith(
        expect.objectContaining({ from: '2026-10-01T00:00:00.000Z', to: '2026-10-02T23:59:59.999Z' }),
        expect.anything(),
      ),
    )
  })

  it('pages through the log', async () => {
    vi.mocked(listAuditLogs).mockResolvedValue({ items: entries, page: 1, pageSize: 20, totalCount: 45 })
    renderPage()
    await screen.findByText('Page 1 of 3')

    fireEvent.click(screen.getByRole('button', { name: 'Next' }))

    await waitFor(() => expect(listAuditLogs).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2 }), expect.anything()))
  })

  it('says so when nothing matches', async () => {
    vi.mocked(listAuditLogs).mockResolvedValue(page([]))
    renderPage()

    expect(await screen.findByText('No log entries found.')).toBeInTheDocument()
  })
})

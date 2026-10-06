import { afterEach, describe, expect, it, vi } from 'vitest'
import { auditActions, listAuditLogs } from './audit-logs'

function fakeFetch(body: unknown = { items: [], page: 1, pageSize: 20, totalCount: 0 }) {
  const fetchMock = vi.fn().mockResolvedValue(
    new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } }),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('audit logs API', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lists the log with GET /api/audit-logs', async () => {
    const fetchMock = fakeFetch()

    await listAuditLogs({})

    expect(fetchMock.mock.calls[0][0]).toBe('/api/audit-logs')
  })

  it('sends only the filters that are set', async () => {
    const fetchMock = fakeFetch()

    await listAuditLogs({ userId: 'u-1', action: 'login.failed', from: '2026-10-01T00:00:00Z', page: 2, pageSize: 50 })

    const url = new URL(String(fetchMock.mock.calls[0][0]), 'http://localhost')
    expect(Object.fromEntries(url.searchParams)).toEqual({
      userId: 'u-1',
      action: 'login.failed',
      from: '2026-10-01T00:00:00Z',
      page: '2',
      pageSize: '50',
    })
  })

  it('knows the audited actions of the server', () => {
    expect(auditActions).toContain('login.failed')
    expect(auditActions).toContain('sla-policy.updated')
  })
})

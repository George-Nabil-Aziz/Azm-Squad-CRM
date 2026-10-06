import { afterEach, describe, expect, it, vi } from 'vitest'
import { exportTicketReport, getTicketReport, reportPath } from './reports'

function fakeFetch(response: Response) {
  const fetchMock = vi.fn().mockResolvedValue(response)
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

const json = (body: unknown) =>
  new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })

describe('reports API', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('builds the path with only the filters that are set', () => {
    expect(reportPath('/api/reports/tickets', {})).toBe('/api/reports/tickets')
    expect(reportPath('/api/reports/tickets', { from: '2026-10-01', status: 'open', channel: undefined, priority: '' })).toBe(
      '/api/reports/tickets?from=2026-10-01&status=open',
    )
  })

  it('reads the ticket report with GET /api/reports/tickets', async () => {
    const fetchMock = fakeFetch(json({ total: 3 }))

    const report = await getTicketReport({ from: '2026-10-01', to: '2026-10-03', categoryId: 'c-1' })

    expect(report).toEqual({ total: 3 })
    expect(fetchMock.mock.calls[0][0]).toBe('/api/reports/tickets?from=2026-10-01&to=2026-10-03&categoryId=c-1')
  })

  it('downloads the export as a file with the chosen format', async () => {
    const fetchMock = fakeFetch(new Response('a,b', { status: 200, headers: { 'Content-Type': 'text/csv' } }))

    const blob = await exportTicketReport({ status: 'open' }, 'csv')

    expect(fetchMock.mock.calls[0][0]).toBe('/api/reports/tickets/export?status=open&format=csv')
    expect(await blob.text()).toBe('a,b')
  })
})

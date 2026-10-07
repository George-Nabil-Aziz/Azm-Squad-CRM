import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  createDepartment,
  listDepartments,
  listDepartmentSlaPolicies,
  removeDepartmentSlaPolicy,
  setDepartmentSlaPolicy,
  transferTicketDepartment,
  updateDepartment,
} from './departments'

function fakeFetch(status = 200, body: unknown = {}) {
  const fetchMock = vi.fn().mockResolvedValue(
    new Response(status === 204 ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function sent(fetchMock: ReturnType<typeof vi.fn>) {
  const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit]
  return { path, method: init.method, body: init.body === undefined ? undefined : JSON.parse(String(init.body)) }
}

describe('departments API', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lists every department, or only the active ones', async () => {
    const all = fakeFetch(200, [])
    await listDepartments({})
    expect(sent(all)).toMatchObject({ path: '/api/departments', method: 'GET' })

    const active = fakeFetch(200, [])
    await listDepartments({ activeOnly: true })
    expect(sent(active).path).toBe('/api/departments?activeOnly=true')
  })

  it('creates and updates a department', async () => {
    const created = fakeFetch(201, { id: 'd1' })
    await createDepartment({ name: 'Billing', isActive: true })
    expect(sent(created)).toEqual({ path: '/api/departments', method: 'POST', body: { name: 'Billing', isActive: true } })

    const updated = fakeFetch(200, { id: 'd1' })
    await updateDepartment('d1', { name: 'Finance', isActive: false })
    expect(sent(updated)).toEqual({ path: '/api/departments/d1', method: 'PUT', body: { name: 'Finance', isActive: false } })
  })

  it('reads, sets and removes the SLA overrides of a department', async () => {
    const list = fakeFetch(200, [])
    await listDepartmentSlaPolicies('d1')
    expect(sent(list)).toMatchObject({ path: '/api/departments/d1/sla-policies', method: 'GET' })

    const set = fakeFetch(200, {})
    await setDepartmentSlaPolicy('d1', 'high', { responseMinutes: 30, resolutionMinutes: 120 })
    expect(sent(set)).toEqual({
      path: '/api/departments/d1/sla-policies/high',
      method: 'PUT',
      body: { responseMinutes: 30, resolutionMinutes: 120 },
    })

    const remove = fakeFetch(204)
    await removeDepartmentSlaPolicy('d1', 'high')
    expect(sent(remove)).toMatchObject({ path: '/api/departments/d1/sla-policies/high', method: 'DELETE' })
  })

  it('transfers a ticket with PUT /api/tickets/{id}/department (null = general)', async () => {
    const fetchMock = fakeFetch(200, { ticketId: 't1', departmentId: null, departmentName: null })

    await transferTicketDepartment('t1', null)

    expect(sent(fetchMock)).toEqual({ path: '/api/tickets/t1/department', method: 'PUT', body: { departmentId: null } })
  })
})

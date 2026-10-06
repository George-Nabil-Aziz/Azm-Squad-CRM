import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import {
  createDepartment,
  listDepartments,
  listDepartmentSlaPolicies,
  removeDepartmentSlaPolicy,
  setDepartmentSlaPolicy,
  updateDepartment,
  type Department,
} from '@/api/departments'
import { ApiError } from '@/api/errors'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { DepartmentsPage } from './DepartmentsPage'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))

vi.mock('@/api/departments', () => ({
  listDepartments: vi.fn(),
  createDepartment: vi.fn(),
  updateDepartment: vi.fn(),
  listDepartmentSlaPolicies: vi.fn(),
  setDepartmentSlaPolicy: vi.fn(),
  removeDepartmentSlaPolicy: vi.fn(),
}))

const superAdmin: CurrentUser = {
  id: '1',
  email: 'admin@crm.local',
  fullName: 'System Administrator',
  roles: ['SuperAdmin'],
  permissions: [permissions.ticketsView, permissions.departmentsManage, permissions.slaManage],
}
/** An admin may manage departments but not the SLA overrides (sla.manage is SuperAdmin only). */
const admin: CurrentUser = { ...superAdmin, id: '4', roles: ['Admin'], permissions: [permissions.ticketsView, permissions.departmentsManage] }
const agent: CurrentUser = { ...superAdmin, id: '2', roles: ['Agent'], permissions: [permissions.ticketsView] }

const billing: Department = {
  id: 'd1',
  name: 'Billing',
  isActive: true,
  createdAt: '2026-10-01T08:00:00Z',
  updatedAt: '2026-10-01T08:00:00Z',
}
const legacy: Department = { ...billing, id: 'd2', name: 'Legacy', isActive: false }

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <DepartmentsPage />
      <ApiErrorToaster />
    </QueryClientProvider>,
  )
}

function rowOf(name: string) {
  return screen.getByRole('row', { name: new RegExp(name) })
}

describe('DepartmentsPage', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(superAdmin)
    vi.mocked(listDepartments).mockReset().mockResolvedValue([billing, legacy])
    vi.mocked(createDepartment).mockReset()
    vi.mocked(updateDepartment).mockReset()
    vi.mocked(listDepartmentSlaPolicies).mockReset().mockResolvedValue([])
    vi.mocked(setDepartmentSlaPolicy).mockReset()
    vi.mocked(removeDepartmentSlaPolicy).mockReset()
  })

  it('lists every department with its status', async () => {
    renderPage()

    expect(screen.getByRole('heading', { level: 1, name: 'Departments' })).toBeInTheDocument()
    expect(await within(await screen.findByRole('row', { name: /Billing/ })).findByText('Active')).toBeInTheDocument()
    expect(within(rowOf('Legacy')).getByText('Inactive')).toBeInTheDocument()
  })

  it('says so when there is no department yet', async () => {
    vi.mocked(listDepartments).mockResolvedValue([])
    renderPage()

    expect(await screen.findByText('No departments yet.')).toBeInTheDocument()
  })

  it('adds a department and reloads the list', async () => {
    vi.mocked(createDepartment).mockResolvedValue({ ...billing, id: 'd3', name: 'Support' })
    renderPage()
    await screen.findByRole('row', { name: /Billing/ })

    fireEvent.click(await screen.findByRole('button', { name: 'Add department' }))
    const dialog = await screen.findByRole('dialog', { name: 'New department' })
    fireEvent.change(within(dialog).getByLabelText('Name'), { target: { value: '  Support ' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(createDepartment).toHaveBeenCalledWith({ name: 'Support', isActive: true })
    expect(await screen.findByText('Department Support was created.')).toBeInTheDocument()
    expect(listDepartments).toHaveBeenCalledTimes(2)
  })

  it('requires a name before calling the API, and shows the server message for a duplicate', async () => {
    vi.mocked(createDepartment).mockRejectedValue(
      new ApiError('POST /api/departments failed with status 400', 400, {
        status: 400,
        errors: { name: ['A department with this name already exists.'] },
      }),
    )
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Add department' }))
    const dialog = await screen.findByRole('dialog', { name: 'New department' })

    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
    expect(await within(dialog).findByText('Enter the department name.')).toBeInTheDocument()
    expect(createDepartment).not.toHaveBeenCalled()

    fireEvent.change(within(dialog).getByLabelText('Name'), { target: { value: 'billing' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
    expect(await within(dialog).findByText('A department with this name already exists.')).toBeInTheDocument()
  })

  it('deactivates a department from the edit dialog', async () => {
    vi.mocked(updateDepartment).mockResolvedValue({ ...billing, isActive: false })
    renderPage()
    await screen.findByRole('row', { name: /Billing/ })

    fireEvent.click(await within(rowOf('Billing')).findByRole('button', { name: 'Edit' }))
    const dialog = await screen.findByRole('dialog', { name: 'Edit department' })
    fireEvent.click(within(dialog).getByRole('checkbox', { name: 'Active (tickets can be put in it)' }))
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(updateDepartment).toHaveBeenCalledWith('d1', { name: 'Billing', isActive: false }))
  })

  it('sets and removes a per-department SLA override', async () => {
    vi.mocked(listDepartmentSlaPolicies).mockResolvedValue([
      { departmentId: 'd1', priority: 'low', responseMinutes: 60, resolutionMinutes: 240, updatedAt: '2026-10-01T08:00:00Z' },
    ])
    vi.mocked(setDepartmentSlaPolicy).mockResolvedValue({
      departmentId: 'd1', priority: 'high', responseMinutes: 30, resolutionMinutes: 90, updatedAt: '2026-10-01T08:00:00Z',
    })
    vi.mocked(removeDepartmentSlaPolicy).mockResolvedValue()
    renderPage()
    await screen.findByRole('row', { name: /Billing/ })

    fireEvent.click(within(rowOf('Billing')).getByRole('button', { name: 'SLA' }))
    const dialog = await screen.findByRole('dialog', { name: 'SLA of Billing' })
    const high = await within(dialog).findByRole('group', { name: 'High' })
    fireEvent.change(within(high).getByLabelText('Response time (minutes)'), { target: { value: '30' } })
    fireEvent.change(within(high).getByLabelText('Resolution time (minutes)'), { target: { value: '90' } })
    fireEvent.click(within(high).getByRole('button', { name: 'Save' }))
    await waitFor(() =>
      expect(setDepartmentSlaPolicy).toHaveBeenCalledWith('d1', 'high', { responseMinutes: 30, resolutionMinutes: 90 }),
    )

    const low = within(dialog).getByRole('group', { name: 'Low' })
    expect(within(low).getByLabelText('Response time (minutes)')).toHaveValue(60)
    fireEvent.click(within(low).getByRole('button', { name: 'Use the global policy' }))
    await waitFor(() => expect(removeDepartmentSlaPolicy).toHaveBeenCalledWith('d1', 'low'))
  })

  it('hides the SLA action from an admin and every action from an agent', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue(admin)
    renderPage()
    await screen.findByRole('row', { name: /Billing/ })
    await waitFor(() => expect(within(rowOf('Billing')).getByRole('button', { name: 'Edit' })).toBeInTheDocument())
    expect(within(rowOf('Billing')).queryByRole('button', { name: 'SLA' })).not.toBeInTheDocument()
  })

  it('hides add and edit from a user without departments.manage', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue(agent)
    renderPage()
    await screen.findByRole('row', { name: /Billing/ })
    await waitFor(() => expect(getCurrentUser).toHaveBeenCalled())

    expect(screen.queryByRole('button', { name: 'Add department' })).not.toBeInTheDocument()
    expect(within(rowOf('Billing')).queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument()
  })
})

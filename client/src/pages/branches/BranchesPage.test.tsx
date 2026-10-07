import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { createBranch, listBranches, updateBranch, type Branch } from '@/api/branches'
import { ApiError } from '@/api/errors'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { BranchesPage } from './BranchesPage'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))
vi.mock('@/api/branches', () => ({ listBranches: vi.fn(), createBranch: vi.fn(), updateBranch: vi.fn() }))

const superAdmin: CurrentUser = {
  id: '1',
  email: 'admin@crm.local',
  fullName: 'System Administrator',
  roles: ['SuperAdmin'],
  permissions: [permissions.branchesManage],
}
const agent: CurrentUser = { ...superAdmin, id: '2', roles: ['Agent'], permissions: [permissions.ticketsView] }

const riyadh: Branch = {
  id: 'b1',
  name: 'Riyadh',
  isActive: true,
  createdAt: '2026-10-01T08:00:00Z',
  updatedAt: '2026-10-01T08:00:00Z',
}
const old: Branch = { ...riyadh, id: 'b2', name: 'Old office', isActive: false }

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <BranchesPage />
      <ApiErrorToaster />
    </QueryClientProvider>,
  )
}

const rowOf = (name: string) => screen.getByRole('row', { name: new RegExp(name) })

describe('BranchesPage', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(superAdmin)
    vi.mocked(listBranches).mockReset().mockResolvedValue([riyadh, old])
    vi.mocked(createBranch).mockReset()
    vi.mocked(updateBranch).mockReset()
  })

  it('lists every branch with its status', async () => {
    renderPage()

    expect(screen.getByRole('heading', { level: 1, name: 'Branches' })).toBeInTheDocument()
    expect(await within(await screen.findByRole('row', { name: /Riyadh/ })).findByText('Active')).toBeInTheDocument()
    expect(within(rowOf('Old office')).getByText('Inactive')).toBeInTheDocument()
  })

  it('adds a branch and reloads the list', async () => {
    vi.mocked(createBranch).mockResolvedValue({ ...riyadh, id: 'b3', name: 'Jeddah' })
    renderPage()
    await screen.findByRole('row', { name: /Riyadh/ })

    fireEvent.click(await screen.findByRole('button', { name: 'Add branch' }))
    const dialog = await screen.findByRole('dialog', { name: 'New branch' })
    fireEvent.change(within(dialog).getByLabelText('Name'), { target: { value: '  Jeddah ' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(createBranch).toHaveBeenCalledWith({ name: 'Jeddah', isActive: true }))
    expect(await screen.findByText('Branch Jeddah was created.')).toBeInTheDocument()
    expect(listBranches).toHaveBeenCalledTimes(2)
  })

  it('requires a name and shows the server message for a duplicate', async () => {
    vi.mocked(createBranch).mockRejectedValue(
      new ApiError('POST /api/branches failed with status 400', 400, {
        status: 400,
        errors: { name: ['A branch with this name already exists.'] },
      }),
    )
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Add branch' }))
    const dialog = await screen.findByRole('dialog', { name: 'New branch' })

    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
    expect(await within(dialog).findByText('Enter the branch name.')).toBeInTheDocument()

    fireEvent.change(within(dialog).getByLabelText('Name'), { target: { value: 'riyadh' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
    expect(await within(dialog).findByText('A branch with this name already exists.')).toBeInTheDocument()
  })

  it('deactivates a branch from the edit dialog', async () => {
    vi.mocked(updateBranch).mockResolvedValue({ ...riyadh, isActive: false })
    renderPage()
    await screen.findByRole('row', { name: /Riyadh/ })

    fireEvent.click(await within(rowOf('Riyadh')).findByRole('button', { name: 'Edit' }))
    const dialog = await screen.findByRole('dialog', { name: 'Edit branch' })
    fireEvent.click(within(dialog).getByRole('checkbox'))
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(updateBranch).toHaveBeenCalledWith('b1', { name: 'Riyadh', isActive: false }))
  })

  it('hides add and edit without branches.manage', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue(agent)
    renderPage()
    await screen.findByRole('row', { name: /Riyadh/ })
    await waitFor(() => expect(getCurrentUser).toHaveBeenCalled())

    expect(screen.queryByRole('button', { name: 'Add branch' })).not.toBeInTheDocument()
    expect(within(rowOf('Riyadh')).queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument()
  })
})

import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { ApiError } from '@/api/errors'
import { listSlaPolicies, updateSlaPolicy, type SlaPolicy } from '@/api/sla-policies'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { SlaPoliciesPage } from './SlaPoliciesPage'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))

vi.mock('@/api/sla-policies', () => ({
  listSlaPolicies: vi.fn(),
  updateSlaPolicy: vi.fn(),
}))

const signedInSuperAdmin: CurrentUser = {
  id: '1',
  email: 'admin@crm.local',
  fullName: 'System Administrator',
  roles: ['SuperAdmin'],
  permissions: [permissions.ticketsView, permissions.slaManage],
}
const signedInAdmin: CurrentUser = { ...signedInSuperAdmin, id: '4', roles: ['Admin'], permissions: [permissions.ticketsView] }

const updatedAt = '2026-10-06T00:00:00Z'
const policies: SlaPolicy[] = [
  { priority: 'high', responseMinutes: 120, resolutionMinutes: 480, updatedAt },
  { priority: 'mid', responseMinutes: 240, resolutionMinutes: 1440, updatedAt },
  { priority: 'low', responseMinutes: 45, resolutionMinutes: 90, updatedAt },
]

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <SlaPoliciesPage />
      <ApiErrorToaster />
    </QueryClientProvider>,
  )
}

function rowOf(name: string) {
  return screen.getByRole('row', { name: new RegExp(name) })
}

async function openEditHigh() {
  renderPage()
  await screen.findByRole('row', { name: /High/ })
  fireEvent.click(await within(rowOf('High')).findByRole('button', { name: 'Edit' }))
  return screen.findByRole('dialog', { name: 'Edit SLA: High' })
}

function fill(dialog: HTMLElement, response: string, resolution: string) {
  fireEvent.change(within(dialog).getByLabelText('Response time (minutes)'), { target: { value: response } })
  fireEvent.change(within(dialog).getByLabelText('Resolution time (minutes)'), { target: { value: resolution } })
  fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
}

describe('SlaPoliciesPage', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(signedInSuperAdmin)
    vi.mocked(listSlaPolicies).mockReset().mockResolvedValue(policies)
    vi.mocked(updateSlaPolicy).mockReset()
  })

  it('lists the policy of every priority with readable times', async () => {
    renderPage()

    expect(screen.getByRole('heading', { level: 1, name: 'SLA policy' })).toBeInTheDocument()
    const high = await screen.findByRole('row', { name: /High/ })
    expect(within(high).getByText('2 h')).toBeInTheDocument()
    expect(within(high).getByText('8 h')).toBeInTheDocument()
    expect(within(rowOf('Mid')).getByText('24 h')).toBeInTheDocument()
    expect(within(rowOf('Low')).getByText('45 min')).toBeInTheDocument()
    expect(within(rowOf('Low')).getByText('1 h 30 min')).toBeInTheDocument()
  })

  it('saves new times for High (1 h / 4 h) and reloads the list', async () => {
    vi.mocked(updateSlaPolicy).mockResolvedValue({ ...policies[0], responseMinutes: 60, resolutionMinutes: 240 })
    const dialog = await openEditHigh()
    expect(within(dialog).getByLabelText('Response time (minutes)')).toHaveValue(120)

    fill(dialog, '60', '240')

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(updateSlaPolicy).toHaveBeenCalledWith('high', { responseMinutes: 60, resolutionMinutes: 240 })
    expect(await screen.findByText('SLA for High was saved.')).toBeInTheDocument()
    expect(listSlaPolicies).toHaveBeenCalledTimes(2)
  })

  it('requires times greater than 0 before calling the API', async () => {
    const dialog = await openEditHigh()

    fill(dialog, '0', '240')

    expect(await within(dialog).findByText('Enter a whole number of minutes greater than 0.')).toBeInTheDocument()
    expect(updateSlaPolicy).not.toHaveBeenCalled()
  })

  it('requires the resolution time to be at least the response time', async () => {
    const dialog = await openEditHigh()

    fill(dialog, '240', '60')

    expect(await within(dialog).findByText('The resolution time must be at least the response time.')).toBeInTheDocument()
    expect(updateSlaPolicy).not.toHaveBeenCalled()
  })

  it('shows the server message under the field on 400', async () => {
    vi.mocked(updateSlaPolicy).mockRejectedValue(
      new ApiError('PUT /api/sla-policies/high failed with status 400', 400, {
        status: 400,
        errors: { responseMinutes: ['Response time is too large.'] },
      }),
    )
    const dialog = await openEditHigh()

    fill(dialog, '60', '240')

    expect(await within(dialog).findByText('Response time is too large.')).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Response time (minutes)')).toHaveAttribute('aria-invalid', 'true')
  })

  it('hides edit from a user without sla.manage', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue(signedInAdmin)
    renderPage()
    await screen.findByRole('row', { name: /High/ })
    await waitFor(() => expect(getCurrentUser).toHaveBeenCalled())

    expect(within(rowOf('High')).queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument()
  })
})

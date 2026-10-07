import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { listDepartments, transferTicketDepartment, type Department } from '@/api/departments'
import { ApiError } from '@/api/errors'
import type { Ticket } from '@/api/tickets'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { TicketDepartmentControl } from './TicketDepartmentControl'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))
vi.mock('@/api/departments', () => ({ listDepartments: vi.fn(), transferTicketDepartment: vi.fn() }))

const user: CurrentUser = {
  id: '2',
  email: 'agent@crm.local',
  fullName: 'Sara Agent',
  roles: ['Agent'],
  permissions: [permissions.ticketsView, permissions.ticketsManage],
}

const department = (id: string, name: string): Department => ({
  id,
  name,
  isActive: true,
  createdAt: '2026-10-01T08:00:00Z',
  updatedAt: '2026-10-01T08:00:00Z',
})

const ticket = { id: 't1', departmentId: 'd1', departmentName: 'Billing' } as Ticket

function renderControl(current: Ticket = ticket) {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <TicketDepartmentControl ticket={current} />
      <ApiErrorToaster />
    </QueryClientProvider>,
  )
}

describe('TicketDepartmentControl', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(user)
    vi.mocked(listDepartments).mockReset().mockResolvedValue([department('d1', 'Billing'), department('d2', 'Support')])
    vi.mocked(transferTicketDepartment).mockReset()
  })

  it('shows the current department and transfers the ticket to another one', async () => {
    vi.mocked(transferTicketDepartment).mockResolvedValue({ ticketId: 't1', departmentId: 'd2', departmentName: 'Support' })
    renderControl()

    const select = await screen.findByRole('combobox', { name: 'Department' })
    await screen.findByRole('option', { name: 'Support' })
    expect(select).toHaveValue('d1')
    fireEvent.change(select, { target: { value: 'd2' } })

    await waitFor(() => expect(transferTicketDepartment).toHaveBeenCalledWith('t1', 'd2'))
    expect(await screen.findByText('The ticket was moved.')).toBeInTheDocument()
  })

  it('moves the ticket to "general" with an empty choice', async () => {
    vi.mocked(transferTicketDepartment).mockResolvedValue({ ticketId: 't1', departmentId: null, departmentName: null })
    renderControl()

    const select = await screen.findByRole('combobox', { name: 'Department' })
    fireEvent.change(select, { target: { value: '' } })

    await waitFor(() => expect(transferTicketDepartment).toHaveBeenCalledWith('t1', null))
  })

  it('shows the server message when the target is refused', async () => {
    vi.mocked(transferTicketDepartment).mockRejectedValue(
      new ApiError('PUT failed with status 400', 400, { status: 400, errors: { departmentId: ['Choose an active department.'] } }),
    )
    renderControl()

    const select = await screen.findByRole('combobox', { name: 'Department' })
    await screen.findByRole('option', { name: 'Support' })
    fireEvent.change(select, { target: { value: 'd2' } })

    expect(await screen.findByRole('alert')).toHaveTextContent('Choose an active department.')
  })

  it('is hidden without tickets.manage', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue({ ...user, permissions: [permissions.ticketsView] })
    renderControl()

    await waitFor(() => expect(getCurrentUser).toHaveBeenCalled())
    expect(screen.queryByRole('combobox', { name: 'Department' })).not.toBeInTheDocument()
  })
})

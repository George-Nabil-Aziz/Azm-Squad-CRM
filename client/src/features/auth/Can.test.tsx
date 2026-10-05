import { QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser } from '@/api/auth'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { Can } from './Can'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))

function renderCan() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <Can permission={permissions.ticketsView}>
        <p>Ticket list</p>
      </Can>
      <Can permission={permissions.ticketsAssign}>
        <button type="button">Assign</button>
      </Can>
    </QueryClientProvider>,
  )
}

describe('Can', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset()
  })

  it('shows its content when the user has the permission', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue({
      id: '1',
      email: 'lead@crm.local',
      fullName: 'Team Lead',
      roles: ['Supervisor'],
      permissions: [permissions.ticketsView, permissions.ticketsAssign],
    })
    renderCan()

    expect(await screen.findByRole('button', { name: 'Assign' })).toBeInTheDocument()
  })

  it('hides its content when the user lacks the permission', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue({
      id: '2',
      email: 'agent@crm.local',
      fullName: 'Sara Agent',
      roles: ['Agent'],
      permissions: [permissions.ticketsView],
    })
    renderCan()

    expect(await screen.findByText('Ticket list')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Assign' })).not.toBeInTheDocument()
  })

  it('hides its content while the permissions are loading', () => {
    vi.mocked(getCurrentUser).mockReturnValue(new Promise(() => {}))
    renderCan()

    expect(screen.queryByText('Ticket list')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Assign' })).not.toBeInTheDocument()
  })
})

import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  getAssignmentSettings,
  setAgentOnDuty,
  setAutoAssign,
  type AssignmentSettings,
} from '@/api/assignment-settings'
import { createQueryClient } from '@/app/query-client'
import { AssignmentSettingsPage } from './AssignmentSettingsPage'

vi.mock('@/api/assignment-settings', () => ({
  getAssignmentSettings: vi.fn(),
  setAutoAssign: vi.fn(),
  setAgentOnDuty: vi.fn(),
}))

const settings: AssignmentSettings = {
  autoAssignEnabled: false,
  agents: [
    { id: 'a1', fullName: 'Sara Agent', onDuty: true, openTickets: 3 },
    { id: 'a2', fullName: 'Omar Agent', onDuty: false, openTickets: 0 },
  ],
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <AssignmentSettingsPage />
    </QueryClientProvider>,
  )
}

describe('AssignmentSettingsPage', () => {
  beforeEach(() => {
    vi.mocked(getAssignmentSettings).mockReset().mockResolvedValue(settings)
    vi.mocked(setAutoAssign).mockReset().mockResolvedValue({ ...settings, autoAssignEnabled: true })
    vi.mocked(setAgentOnDuty).mockReset().mockResolvedValue(settings)
  })

  it('lists the agents with their open tickets and duty', async () => {
    renderPage()

    expect(screen.getByRole('heading', { level: 1, name: 'Assignment' })).toBeInTheDocument()
    const sara = await screen.findByRole('row', { name: /Sara Agent/ })
    expect(within(sara).getByText('3')).toBeInTheDocument()
    expect(within(sara).getByRole('checkbox', { name: 'On duty: Sara Agent' })).toBeChecked()
    expect(within(screen.getByRole('row', { name: /Omar Agent/ })).getByRole('checkbox')).not.toBeChecked()
  })

  it('turns automatic assignment on', async () => {
    renderPage()
    const toggle = await screen.findByRole('checkbox', { name: 'Assign new tickets automatically' })
    expect(toggle).not.toBeChecked()

    fireEvent.click(toggle)

    await waitFor(() => expect(setAutoAssign).toHaveBeenCalledWith(true))
  })

  it('sets an agent off duty', async () => {
    renderPage()

    fireEvent.click(await screen.findByRole('checkbox', { name: 'On duty: Sara Agent' }))

    await waitFor(() => expect(setAgentOnDuty).toHaveBeenCalledWith('a1', false))
  })
})

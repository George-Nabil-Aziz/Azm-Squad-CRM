import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { generateTicketSummary, getAiStatus, getTicketSummary } from '@/api/ai'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { ApiError } from '@/api/errors'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { TicketSummary } from './TicketSummary'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))
vi.mock('@/api/ai', () => ({ getAiStatus: vi.fn(), getTicketSummary: vi.fn(), generateTicketSummary: vi.fn() }))

const agent: CurrentUser = {
  id: '2',
  email: 'agent@crm.local',
  fullName: 'Sara Agent',
  roles: ['Agent'],
  permissions: [permissions.ticketsView, permissions.ticketsManage],
}
const viewer: CurrentUser = { ...agent, permissions: [permissions.ticketsView] }

function renderSummary() {
  render(
    <QueryClientProvider client={createQueryClient()}>
      <TicketSummary ticketId="t1" />
      <ApiErrorToaster />
    </QueryClientProvider>,
  )
}

describe('AI ticket summary', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(agent)
    vi.mocked(getAiStatus).mockReset().mockResolvedValue({ enabled: true })
    vi.mocked(getTicketSummary).mockReset().mockResolvedValue({ text: null, language: null, generatedAt: null })
    vi.mocked(generateTicketSummary).mockReset()
  })

  it('generates a summary with one click and shows it', async () => {
    vi.mocked(generateTicketSummary).mockResolvedValue({
      text: '- Duplicate charge on line 3',
      language: 'en',
      generatedAt: '2026-10-01T08:00:00Z',
    })
    renderSummary()

    fireEvent.click(await screen.findByRole('button', { name: 'Summarize with AI' }))

    expect(await screen.findByText('- Duplicate charge on line 3')).toBeInTheDocument()
    expect(generateTicketSummary).toHaveBeenCalledWith('t1')
    expect(screen.getByRole('button', { name: 'Regenerate' })).toBeInTheDocument()
    expect(screen.getByText(/Generated/)).toBeInTheDocument()
  })

  it('shows the saved summary with its time, and regenerating replaces it', async () => {
    vi.mocked(getTicketSummary).mockResolvedValue({ text: 'Old summary', language: 'en', generatedAt: '2026-10-01T08:00:00Z' })
    vi.mocked(generateTicketSummary).mockResolvedValue({ text: 'New summary', language: 'en', generatedAt: '2026-10-01T09:00:00Z' })
    renderSummary()

    expect(await screen.findByText('Old summary')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Regenerate' }))

    expect(await screen.findByText('New summary')).toBeInTheDocument()
    expect(screen.queryByText('Old summary')).not.toBeInTheDocument()
  })

  it('shows an error and keeps the old summary when the AI call fails', async () => {
    vi.mocked(getTicketSummary).mockResolvedValue({ text: 'Old summary', language: 'en', generatedAt: '2026-10-01T08:00:00Z' })
    vi.mocked(generateTicketSummary).mockRejectedValue(
      new ApiError('POST failed with status 502', 502, { status: 502, title: 'AI request failed' }),
    )
    renderSummary()

    fireEvent.click(await screen.findByRole('button', { name: 'Regenerate' }))

    await waitFor(() => expect(generateTicketSummary).toHaveBeenCalled())
    expect(await screen.findByText('Old summary')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Regenerate' })).toBeEnabled()
  })

  it('is hidden when AI is not configured', async () => {
    vi.mocked(getAiStatus).mockResolvedValue({ enabled: false })
    renderSummary()

    await waitFor(() => expect(getAiStatus).toHaveBeenCalled())
    expect(screen.queryByRole('region', { name: 'AI summary' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Summarize with AI' })).not.toBeInTheDocument()
    expect(getTicketSummary).not.toHaveBeenCalled()
  })

  it('shows the saved summary but no button to someone who may not manage tickets', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue(viewer)
    vi.mocked(getTicketSummary).mockResolvedValue({ text: 'Saved summary', language: 'en', generatedAt: '2026-10-01T08:00:00Z' })
    renderSummary()

    expect(await screen.findByText('Saved summary')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Regenerate' })).not.toBeInTheDocument()
  })

  it('writes an Arabic summary right to left', async () => {
    vi.mocked(getTicketSummary).mockResolvedValue({ text: 'ملخص', language: 'ar', generatedAt: '2026-10-01T08:00:00Z' })
    renderSummary()

    expect(await screen.findByText('ملخص')).toHaveAttribute('dir', 'rtl')
  })
})

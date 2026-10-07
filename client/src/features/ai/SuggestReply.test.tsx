import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { generateReplyDraft, getAiStatus } from '@/api/ai'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { ApiError } from '@/api/errors'
import { addTicketMessage } from '@/api/tickets'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { TicketReplyForm } from '@/features/tickets/TicketReplyForm'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))
vi.mock('@/api/tickets', () => ({ addTicketMessage: vi.fn(), listTicketAssignees: vi.fn().mockResolvedValue([]) }))
vi.mock('@/api/knowledge-base', () => ({ searchKb: vi.fn(), linkTicketArticle: vi.fn(), listTicketArticles: vi.fn() }))
vi.mock('@/api/quick-replies', () => ({ listQuickReplies: vi.fn().mockResolvedValue([]) }))
vi.mock('@/api/ai', () => ({ getAiStatus: vi.fn(), generateReplyDraft: vi.fn(), getSuggestions: vi.fn().mockResolvedValue([]) }))

const agent: CurrentUser = {
  id: '2',
  email: 'agent@crm.local',
  fullName: 'Sara Agent',
  roles: ['Agent'],
  permissions: [permissions.ticketsView, permissions.ticketsManage],
}

function renderForm() {
  render(
    <QueryClientProvider client={createQueryClient()}>
      <TicketReplyForm ticketId="t1" />
      <ApiErrorToaster />
    </QueryClientProvider>,
  )
}

describe('AI suggested reply', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(agent)
    vi.mocked(getAiStatus).mockReset().mockResolvedValue({ enabled: true })
    vi.mocked(generateReplyDraft).mockReset().mockResolvedValue({
      draft: 'Hello,\nPlease use the reset link.\n[Agent name]',
      language: 'en',
      articles: [{ id: 'a1', title: 'Reset your password' }],
    })
    vi.mocked(addTicketMessage).mockReset().mockResolvedValue({} as never)
  })

  it('puts the draft into the reply box for review, and lists the articles it is based on', async () => {
    renderForm()

    fireEvent.click(await screen.findByRole('button', { name: 'Suggest a reply' }))

    await waitFor(() => {
      expect((screen.getByLabelText('Message') as HTMLTextAreaElement).value).toBe('Hello,\nPlease use the reset link.\n[Agent name]')
    })
    expect(generateReplyDraft).toHaveBeenCalledWith('t1')
    expect(screen.getByText('Review the draft before you send it.')).toBeInTheDocument()
    expect(screen.getByText('Reset your password')).toBeInTheDocument()
    expect(addTicketMessage).not.toHaveBeenCalled() // never sent by itself
  })

  it('adds the draft after what the agent already typed', async () => {
    renderForm()
    fireEvent.change(await screen.findByLabelText('Message'), { target: { value: 'Hi Nour,' } })

    fireEvent.click(screen.getByRole('button', { name: 'Suggest a reply' }))

    await waitFor(() => {
      expect((screen.getByLabelText('Message') as HTMLTextAreaElement).value).toBe('Hi Nour,\n\nHello,\nPlease use the reset link.\n[Agent name]')
    })
  })

  it('shows the error and leaves the reply box usable when the AI call fails', async () => {
    vi.mocked(generateReplyDraft).mockRejectedValue(new ApiError('POST failed with status 502', 502, { status: 502, title: 'AI request failed' }))
    renderForm()
    fireEvent.change(await screen.findByLabelText('Message'), { target: { value: 'My own reply' } })

    fireEvent.click(screen.getByRole('button', { name: 'Suggest a reply' }))
    await waitFor(() => expect(generateReplyDraft).toHaveBeenCalled())
    fireEvent.click(await screen.findByRole('button', { name: 'Send reply' }))

    expect((screen.getByLabelText('Message') as HTMLTextAreaElement).value).toBe('My own reply')
    await waitFor(() => expect(addTicketMessage).toHaveBeenCalledWith('t1', expect.objectContaining({ body: 'My own reply' })))
  })

  it('is hidden when AI is not configured', async () => {
    vi.mocked(getAiStatus).mockResolvedValue({ enabled: false })
    renderForm()

    await screen.findByLabelText('Message')
    await waitFor(() => expect(getAiStatus).toHaveBeenCalled())
    expect(screen.queryByRole('button', { name: 'Suggest a reply' })).not.toBeInTheDocument()
  })
})

import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getSuggestions, sendSuggestionFeedback } from '@/api/ai'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { linkTicketArticle } from '@/api/knowledge-base'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { SuggestedSolutions } from './SuggestedSolutions'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))
vi.mock('@/api/ai', () => ({ getSuggestions: vi.fn(), sendSuggestionFeedback: vi.fn() }))
vi.mock('@/api/knowledge-base', () => ({ linkTicketArticle: vi.fn() }))

const agent: CurrentUser = {
  id: '2',
  email: 'agent@crm.local',
  fullName: 'Sara Agent',
  roles: ['Agent'],
  permissions: [permissions.ticketsView, permissions.ticketsManage, permissions.kbView],
}

const suggestions = [
  { articleId: 'a1', title: 'Reset your password', summary: 'Use the reset link.', useful: null },
  { articleId: 'a2', title: 'Account recovery', summary: 'Contact support.', useful: null },
  { articleId: 'a3', title: 'Two factor codes', summary: 'Use a backup code.', useful: true },
]

function renderIt(onInsert = vi.fn()) {
  render(
    <QueryClientProvider client={createQueryClient()}>
      <SuggestedSolutions ticketId="t1" onInsert={onInsert} />
    </QueryClientProvider>,
  )
  return onInsert
}

describe('AI suggested solutions', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(agent)
    vi.mocked(getSuggestions).mockReset().mockResolvedValue(suggestions)
    vi.mocked(sendSuggestionFeedback).mockReset().mockResolvedValue({ articleId: 'a1', useful: true })
    vi.mocked(linkTicketArticle).mockReset().mockResolvedValue({ insertText: '\n\nReset your password\nlink\n', title: 'Reset your password' } as never)
  })

  it('lists the suggested articles', async () => {
    renderIt()

    const region = await screen.findByRole('region', { name: 'Suggested articles' })
    expect(within(region).getAllByRole('listitem')).toHaveLength(3)
    expect(within(region).getByText('Reset your password')).toBeInTheDocument()
  })

  it('inserts a suggestion into the reply', async () => {
    const onInsert = renderIt()

    const item = (await screen.findByText('Reset your password')).closest('li')!
    fireEvent.click(within(item).getByRole('button', { name: 'Insert' }))

    await waitFor(() => expect(linkTicketArticle).toHaveBeenCalledWith('t1', 'a1'))
    await waitFor(() => expect(onInsert).toHaveBeenCalledWith('\n\nReset your password\nlink\n'))
  })

  it('stores useful and not useful feedback and marks the choice', async () => {
    renderIt()

    const item = (await screen.findByText('Account recovery')).closest('li')!
    fireEvent.click(within(item).getByRole('button', { name: 'Not useful' }))

    await waitFor(() => expect(sendSuggestionFeedback).toHaveBeenCalledWith('t1', 'a2', false))
    const useful = within((await screen.findByText('Two factor codes')).closest('li')!).getByRole('button', { name: 'Useful' })
    expect(useful).toHaveAttribute('aria-pressed', 'true')
  })

  it('shows nothing without kb.view, and nothing when there are no suggestions', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue({ ...agent, permissions: [permissions.ticketsView, permissions.ticketsManage] })
    const { unmount } = render(
      <QueryClientProvider client={createQueryClient()}>
        <SuggestedSolutions ticketId="t1" onInsert={vi.fn()} />
      </QueryClientProvider>,
    )
    await waitFor(() => expect(getCurrentUser).toHaveBeenCalled())
    expect(screen.queryByRole('region', { name: 'Suggested articles' })).not.toBeInTheDocument()
    expect(getSuggestions).not.toHaveBeenCalled()
    unmount()

    vi.mocked(getCurrentUser).mockResolvedValue(agent)
    vi.mocked(getSuggestions).mockResolvedValue([])
    renderIt()
    await waitFor(() => expect(getSuggestions).toHaveBeenCalled())
    expect(screen.queryByRole('region', { name: 'Suggested articles' })).not.toBeInTheDocument()
  })
})

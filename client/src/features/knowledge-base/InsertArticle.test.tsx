import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { ApiError } from '@/api/errors'
import { linkTicketArticle, listTicketArticles, searchKb } from '@/api/knowledge-base'
import { addTicketMessage } from '@/api/tickets'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { TicketReplyForm } from '@/features/tickets/TicketReplyForm'
import { LinkedArticles } from './LinkedArticles'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))
vi.mock('@/api/ai', () => ({ getAiStatus: vi.fn().mockResolvedValue({ enabled: false }), generateReplyDraft: vi.fn() }))
vi.mock('@/api/tickets', () => ({ addTicketMessage: vi.fn() }))
vi.mock('@/api/knowledge-base', () => ({
  searchKb: vi.fn(),
  linkTicketArticle: vi.fn(),
  listTicketArticles: vi.fn(),
}))

const agent: CurrentUser = {
  id: '2',
  email: 'agent@crm.local',
  fullName: 'Sara Agent',
  roles: ['Agent'],
  permissions: [permissions.ticketsView, permissions.ticketsManage, permissions.kbView],
}
const noKb: CurrentUser = { ...agent, permissions: [permissions.ticketsView, permissions.ticketsManage] }

function renderWith(children: React.ReactNode) {
  render(
    <QueryClientProvider client={createQueryClient()}>
      {children}
      <ApiErrorToaster />
    </QueryClientProvider>,
  )
}

describe('Insert article into a reply', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(agent)
    vi.mocked(searchKb).mockReset().mockResolvedValue([
      { type: 'article', id: 'a1', title: 'Reset your password', snippet: 'Use the reset link.', score: 30 },
      { type: 'faq', id: 'f1', title: 'How do I reset?', snippet: 'From settings.', score: 13 },
    ])
    vi.mocked(linkTicketArticle).mockReset().mockResolvedValue({
      id: 'l1',
      articleId: 'a1',
      title: 'Reset your password',
      summary: 'Use the reset link.',
      url: 'https://help.example.com/portal/kb/articles/a1',
      insertText: '\n\nReset your password\nUse the reset link.\nhttps://help.example.com/portal/kb/articles/a1\n',
      linkedAt: '2026-10-01T08:00:00Z',
    })
    vi.mocked(listTicketArticles).mockReset().mockResolvedValue([])
    vi.mocked(addTicketMessage).mockReset()
  })

  it('adds the link and summary of the chosen article to the reply', async () => {
    renderWith(<TicketReplyForm ticketId="t1" />)
    fireEvent.change(screen.getByLabelText('Message'), { target: { value: 'Hello Nour,' } })

    fireEvent.click(await screen.findByRole('button', { name: 'Insert article' }))
    fireEvent.change(screen.getByRole('searchbox', { name: 'Search articles to insert' }), { target: { value: 'reset' } })
    fireEvent.keyDown(screen.getByRole('searchbox', { name: 'Search articles to insert' }), { key: 'Enter' })
    const group = await screen.findByRole('group', { name: 'Insert a knowledge base article' })
    const items = await within(group).findAllByRole('listitem')

    expect(items).toHaveLength(1) // FAQs are not articles: only articles can be inserted
    fireEvent.click(within(items[0]).getByRole('button', { name: 'Insert' }))

    await waitFor(() => expect(linkTicketArticle).toHaveBeenCalledWith('t1', 'a1'))
    await waitFor(() => {
      const box = screen.getByLabelText('Message') as HTMLTextAreaElement
      expect(box.value).toBe(
        'Hello Nour,\n\nReset your password\nUse the reset link.\nhttps://help.example.com/portal/kb/articles/a1\n',
      )
    })
    expect(screen.queryByRole('group', { name: 'Insert a knowledge base article' })).not.toBeInTheDocument()
  })

  it('shows the server message when the article cannot be linked (unpublished)', async () => {
    vi.mocked(linkTicketArticle).mockRejectedValue(
      new ApiError('POST failed with status 400', 400, { status: 400, errors: { articleId: ['Publish it first.'] } }),
    )
    renderWith(<TicketReplyForm ticketId="t1" />)
    fireEvent.click(await screen.findByRole('button', { name: 'Insert article' }))
    fireEvent.change(screen.getByRole('searchbox', { name: 'Search articles to insert' }), { target: { value: 'reset' } })
    fireEvent.click(screen.getByRole('button', { name: 'Search' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Insert' }))

    await waitFor(() => expect(linkTicketArticle).toHaveBeenCalled())
    expect((screen.getByLabelText('Message') as HTMLTextAreaElement).value).toBe('')
  })

  it('is hidden for a user without kb.view', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue(noKb)
    renderWith(<TicketReplyForm ticketId="t1" />)

    await screen.findByLabelText('Message')
    await waitFor(() => expect(getCurrentUser).toHaveBeenCalled())
    expect(screen.queryByRole('button', { name: 'Insert article' })).not.toBeInTheDocument()
  })

  it('lists the articles linked to the ticket', async () => {
    vi.mocked(listTicketArticles).mockResolvedValue([
      { id: 'l1', articleId: 'a1', title: 'Reset your password', linkedAt: '2026-10-01T08:00:00Z', linkedById: '2', linkedByName: 'Sara Agent' },
    ])
    renderWith(<LinkedArticles ticketId="t1" />)

    const section = await screen.findByRole('region', { name: 'Linked articles' })
    expect(within(section).getByText('Reset your password')).toBeInTheDocument()
    expect(section).toHaveTextContent('Sara Agent')
  })

  it('shows nothing when no article was linked', async () => {
    renderWith(<LinkedArticles ticketId="t1" />)

    await waitFor(() => expect(listTicketArticles).toHaveBeenCalled())
    expect(screen.queryByRole('region', { name: 'Linked articles' })).not.toBeInTheDocument()
  })
})

import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { listKbArticles, listKbCategories, searchKb } from '@/api/knowledge-base'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { KnowledgeBasePage } from './KnowledgeBasePage'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))
vi.mock('@/api/knowledge-base', () => ({
  listKbCategories: vi.fn(),
  listKbArticles: vi.fn(),
  searchKb: vi.fn(),
}))

const agent: CurrentUser = {
  id: '2',
  email: 'agent@crm.local',
  fullName: 'Sara Agent',
  roles: ['Agent'],
  permissions: [permissions.kbView],
}

function renderPage() {
  render(
    <QueryClientProvider client={createQueryClient()}>
      <KnowledgeBasePage />
    </QueryClientProvider>,
  )
}

describe('Knowledge base search', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(agent)
    vi.mocked(listKbCategories).mockReset().mockResolvedValue([])
    vi.mocked(listKbArticles).mockReset().mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    vi.mocked(searchKb).mockReset()
  })

  it('lists the hits of a search with their type, title and snippet', async () => {
    vi.mocked(searchKb).mockResolvedValue([
      { type: 'article', id: 'a1', title: 'Reset your password', snippet: 'Use the reset link.', score: 30 },
      { type: 'faq', id: 'f1', title: 'How do I reset?', snippet: 'From settings.', score: 13 },
    ])
    renderPage()

    fireEvent.change(screen.getByRole('searchbox', { name: 'Search articles and FAQs' }), { target: { value: ' reset ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Search' }))

    const section = await screen.findByRole('region', { name: 'Search the knowledge base' })
    const items = await within(section).findAllByRole('listitem')
    expect(items).toHaveLength(2)
    expect(items[0]).toHaveTextContent('Article')
    expect(items[0]).toHaveTextContent('Reset your password')
    expect(items[0]).toHaveTextContent('Use the reset link.')
    expect(items[1]).toHaveTextContent('FAQ')
    expect(searchKb).toHaveBeenCalledWith('reset', expect.anything())
  })

  it('does not search when the box is empty', async () => {
    renderPage()

    fireEvent.click(screen.getByRole('button', { name: 'Search' }))

    await waitFor(() => expect(listKbArticles).toHaveBeenCalled())
    expect(searchKb).not.toHaveBeenCalled()
    expect(screen.queryByText('Nothing found.')).not.toBeInTheDocument()
  })

  it('says so when nothing matches', async () => {
    vi.mocked(searchKb).mockResolvedValue([])
    renderPage()

    fireEvent.change(screen.getByRole('searchbox', { name: 'Search articles and FAQs' }), { target: { value: 'xyz' } })
    fireEvent.click(screen.getByRole('button', { name: 'Search' }))

    expect(await screen.findByText('Nothing found.')).toBeInTheDocument()
  })
})

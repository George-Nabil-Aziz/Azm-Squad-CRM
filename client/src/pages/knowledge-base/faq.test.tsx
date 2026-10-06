import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { createKbFaq, deleteKbFaq, listKbArticles, listKbCategories, listKbFaqs, updateKbFaq, type KbFaq } from '@/api/knowledge-base'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { KnowledgeBasePage } from './KnowledgeBasePage'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))
vi.mock('@/api/knowledge-base', () => ({
  listKbCategories: vi.fn(),
  listKbArticles: vi.fn(),
  listKbFaqs: vi.fn(),
  createKbFaq: vi.fn(),
  updateKbFaq: vi.fn(),
  deleteKbFaq: vi.fn(),
}))

const editor: CurrentUser = {
  id: '4',
  email: 'admin2@crm.local',
  fullName: 'Admin User',
  roles: ['Admin'],
  permissions: [permissions.kbView, permissions.kbManage],
}
const agent: CurrentUser = { ...editor, id: '2', roles: ['Agent'], permissions: [permissions.kbView] }

const pay: KbFaq = {
  id: 'f1',
  questionEn: 'How do I pay?',
  answerEn: 'Online.',
  questionAr: null,
  answerAr: null,
  question: 'How do I pay?',
  answer: 'Online.',
  displayOrder: 1,
  isPublished: true,
  createdAt: '2026-10-01T08:00:00Z',
  updatedAt: '2026-10-01T08:00:00Z',
}
const refund: KbFaq = { ...pay, id: 'f2', questionEn: 'Refunds', question: 'Refunds', displayOrder: 2, isPublished: false }

function renderFaqs() {
  render(
    <QueryClientProvider client={createQueryClient()}>
      <KnowledgeBasePage />
      <ApiErrorToaster />
    </QueryClientProvider>,
  )
  fireEvent.click(screen.getByRole('tab', { name: 'FAQs' }))
}

describe('KnowledgeBasePage FAQs tab', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(editor)
    vi.mocked(listKbCategories).mockReset().mockResolvedValue([])
    vi.mocked(listKbArticles).mockReset().mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    vi.mocked(listKbFaqs).mockReset().mockResolvedValue([pay, refund])
    vi.mocked(createKbFaq).mockReset()
    vi.mocked(updateKbFaq).mockReset()
    vi.mocked(deleteKbFaq).mockReset()
  })

  it('lists the FAQs in display order with their published state', async () => {
    renderFaqs()

    const rows = await screen.findAllByRole('row')
    expect(rows[1]).toHaveTextContent('How do I pay?')
    expect(rows[1]).toHaveTextContent('Published')
    expect(rows[2]).toHaveTextContent('Refunds')
    expect(rows[2]).toHaveTextContent('Draft')
  })

  it('creates a FAQ with both languages and a display order', async () => {
    vi.mocked(createKbFaq).mockResolvedValue(pay)
    renderFaqs()
    fireEvent.click(await screen.findByRole('button', { name: 'New FAQ' }))
    const dialog = await screen.findByRole('dialog')

    fireEvent.change(within(dialog).getByLabelText('Question (English)'), { target: { value: 'How do I pay?' } })
    fireEvent.change(within(dialog).getByLabelText('Answer (English)'), { target: { value: 'Online.' } })
    fireEvent.change(within(dialog).getByLabelText('Question (Arabic)'), { target: { value: 'كيف أدفع؟' } })
    fireEvent.change(within(dialog).getByLabelText('Answer (Arabic)'), { target: { value: 'عبر الإنترنت.' } })
    fireEvent.change(within(dialog).getByLabelText(/Display order/), { target: { value: '3' } })
    fireEvent.click(within(dialog).getByRole('checkbox', { name: /Published/ }))
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() =>
      expect(createKbFaq).toHaveBeenCalledWith({
        questionEn: 'How do I pay?',
        answerEn: 'Online.',
        questionAr: 'كيف أدفع؟',
        answerAr: 'عبر الإنترنت.',
        displayOrder: 3,
        isPublished: true,
      }),
    )
  })

  it('sends no display order when the field is empty', async () => {
    vi.mocked(createKbFaq).mockResolvedValue(pay)
    renderFaqs()
    fireEvent.click(await screen.findByRole('button', { name: 'New FAQ' }))
    const dialog = await screen.findByRole('dialog')
    fireEvent.change(within(dialog).getByLabelText('Question (English)'), { target: { value: 'Q' } })
    fireEvent.change(within(dialog).getByLabelText('Answer (English)'), { target: { value: 'A' } })

    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(createKbFaq).toHaveBeenCalledWith(expect.objectContaining({ displayOrder: null, isPublished: false })))
  })

  it('asks for a question before saving', async () => {
    renderFaqs()
    fireEvent.click(await screen.findByRole('button', { name: 'New FAQ' }))
    const dialog = await screen.findByRole('dialog')

    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText('Enter a question and an answer in English or Arabic.')).toBeInTheDocument()
    expect(createKbFaq).not.toHaveBeenCalled()
  })

  it('edits and deletes a FAQ', async () => {
    vi.mocked(updateKbFaq).mockResolvedValue(pay)
    vi.mocked(deleteKbFaq).mockResolvedValue(undefined)
    renderFaqs()
    const row = await screen.findByRole('row', { name: /Refunds/ })

    fireEvent.click(within(row).getByRole('button', { name: 'Edit' }))
    const dialog = await screen.findByRole('dialog')
    fireEvent.change(within(dialog).getByLabelText(/Display order/), { target: { value: '9' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(updateKbFaq).toHaveBeenCalledWith('f2', expect.objectContaining({ displayOrder: 9 })))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())

    fireEvent.click(within(await screen.findByRole('row', { name: /Refunds/ })).getByRole('button', { name: 'Delete' }))
    fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Delete' }))
    await waitFor(() => expect(deleteKbFaq).toHaveBeenCalledWith('f2'))
  })

  it('hides the writing actions from a user without kb.manage', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue(agent)
    vi.mocked(listKbFaqs).mockResolvedValue([pay])
    renderFaqs()

    expect(await screen.findByRole('row', { name: /How do I pay/ })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'New FAQ' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument()
  })

  it('says so when there is no FAQ yet', async () => {
    vi.mocked(listKbFaqs).mockResolvedValue([])
    renderFaqs()

    expect(await screen.findByText('No FAQs yet.')).toBeInTheDocument()
  })
})

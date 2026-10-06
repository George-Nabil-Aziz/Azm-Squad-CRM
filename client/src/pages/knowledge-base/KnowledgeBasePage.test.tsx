import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { ApiError } from '@/api/errors'
import {
  createKbArticle,
  createKbCategory,
  deleteKbArticle,
  deleteKbCategory,
  listKbArticles,
  listKbCategories,
  publishKbArticle,
  unpublishKbArticle,
  updateKbArticle,
  updateKbCategory,
  type KbArticle,
  type KbCategory,
} from '@/api/knowledge-base'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { KnowledgeBasePage } from './KnowledgeBasePage'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))
vi.mock('@/api/knowledge-base', () => ({
  listKbCategories: vi.fn(),
  createKbCategory: vi.fn(),
  updateKbCategory: vi.fn(),
  deleteKbCategory: vi.fn(),
  listKbArticles: vi.fn(),
  createKbArticle: vi.fn(),
  updateKbArticle: vi.fn(),
  publishKbArticle: vi.fn(),
  unpublishKbArticle: vi.fn(),
  deleteKbArticle: vi.fn(),
}))

const editor: CurrentUser = {
  id: '4',
  email: 'admin2@crm.local',
  fullName: 'Admin User',
  roles: ['Admin'],
  permissions: [permissions.kbView, permissions.kbManage],
}
const agent: CurrentUser = { ...editor, id: '2', roles: ['Agent'], permissions: [permissions.kbView] }

const billing: KbCategory = { id: 'c1', nameEn: 'Billing', nameAr: 'الفواتير', name: 'Billing', articleCount: 1 }

const draft: KbArticle = {
  id: 'a1',
  categoryId: 'c1',
  categoryName: 'Billing',
  titleEn: 'Reset your password',
  bodyEn: 'Use the reset link.',
  titleAr: null,
  bodyAr: null,
  title: 'Reset your password',
  status: 'draft',
  publishedAt: null,
  helpfulCount: 0,
  notHelpfulCount: 0,
  linkedCount: 0,
  createdAt: '2026-10-01T08:00:00Z',
  updatedAt: '2026-10-01T08:00:00Z',
}
const live: KbArticle = { ...draft, id: 'a2', titleEn: 'Invoices', title: 'Invoices', status: 'published', publishedAt: '2026-10-02T08:00:00Z' }

function page(items: KbArticle[]) {
  return { items, page: 1, pageSize: 20, totalCount: items.length }
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <KnowledgeBasePage />
      <ApiErrorToaster />
    </QueryClientProvider>,
  )
}

function rowOf(name: string) {
  return screen.getByRole('row', { name: new RegExp(name) })
}

describe('KnowledgeBasePage', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(editor)
    vi.mocked(listKbCategories).mockReset().mockResolvedValue([billing])
    vi.mocked(listKbArticles).mockReset().mockResolvedValue(page([draft, live]))
    vi.mocked(createKbArticle).mockReset()
    vi.mocked(updateKbArticle).mockReset()
    vi.mocked(publishKbArticle).mockReset()
    vi.mocked(unpublishKbArticle).mockReset()
    vi.mocked(deleteKbArticle).mockReset()
    vi.mocked(createKbCategory).mockReset()
    vi.mocked(updateKbCategory).mockReset()
    vi.mocked(deleteKbCategory).mockReset()
  })

  it('lists articles with their status, drafts included for editors', async () => {
    renderPage()

    expect(screen.getByRole('heading', { level: 1, name: 'Knowledge base' })).toBeInTheDocument()
    expect(await within(await screen.findByRole('row', { name: /Reset your password/ })).findByText('Draft')).toBeInTheDocument()
    expect(within(rowOf('Invoices')).getByText('Published')).toBeInTheDocument()
  })

  it('lets an editor create an article with both languages (saved as a draft)', async () => {
    vi.mocked(createKbArticle).mockResolvedValue(draft)
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'New article' }))
    const dialog = await screen.findByRole('dialog')

    await waitFor(() => expect(within(dialog).getByRole('option', { name: 'Billing' })).toBeInTheDocument())
    fireEvent.change(within(dialog).getByLabelText('Category'), { target: { value: 'c1' } })
    fireEvent.change(within(dialog).getByLabelText('Title (English)'), { target: { value: 'Reset your password' } })
    fireEvent.change(within(dialog).getByLabelText('Body (English)'), { target: { value: 'Use the reset link.' } })
    fireEvent.change(within(dialog).getByLabelText('Title (Arabic)'), { target: { value: 'إعادة تعيين كلمة المرور' } })
    fireEvent.change(within(dialog).getByLabelText('Body (Arabic)'), { target: { value: 'استخدم الرابط.' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() =>
      expect(createKbArticle).toHaveBeenCalledWith({
        categoryId: 'c1',
        titleEn: 'Reset your password',
        bodyEn: 'Use the reset link.',
        titleAr: 'إعادة تعيين كلمة المرور',
        bodyAr: 'استخدم الرابط.',
      }),
    )
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('asks for a title before saving', async () => {
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'New article' }))
    const dialog = await screen.findByRole('dialog')
    await waitFor(() => expect(within(dialog).getByRole('option', { name: 'Billing' })).toBeInTheDocument())
    fireEvent.change(within(dialog).getByLabelText('Category'), { target: { value: 'c1' } })

    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText('Enter a title and a body in English or Arabic.')).toBeInTheDocument()
    expect(createKbArticle).not.toHaveBeenCalled()
  })

  it('shows the server field error of a 400 next to the field', async () => {
    vi.mocked(createKbArticle).mockRejectedValue(
      new ApiError('POST /api/kb/articles failed with status 400', 400, {
        status: 400,
        errors: { title: ['The server wants a title.'] },
      }),
    )
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'New article' }))
    const dialog = await screen.findByRole('dialog')
    await waitFor(() => expect(within(dialog).getByRole('option', { name: 'Billing' })).toBeInTheDocument())
    fireEvent.change(within(dialog).getByLabelText('Category'), { target: { value: 'c1' } })
    fireEvent.change(within(dialog).getByLabelText('Title (English)'), { target: { value: 'T' } })
    fireEvent.change(within(dialog).getByLabelText('Body (English)'), { target: { value: 'B' } })

    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText('The server wants a title.')).toBeInTheDocument()
  })

  it('publishes a draft and unpublishes a published article', async () => {
    vi.mocked(publishKbArticle).mockResolvedValue({ ...draft, status: 'published' })
    vi.mocked(unpublishKbArticle).mockResolvedValue({ ...live, status: 'draft' })
    renderPage()
    await screen.findByRole('row', { name: /Reset your password/ })

    fireEvent.click(within(rowOf('Reset your password')).getByRole('button', { name: 'Publish' }))
    await waitFor(() => expect(publishKbArticle).toHaveBeenCalledWith('a1'))
    fireEvent.click(within(rowOf('Invoices')).getByRole('button', { name: 'Unpublish' }))

    await waitFor(() => expect(unpublishKbArticle).toHaveBeenCalledWith('a2'))
  })

  it('deletes an article after confirming', async () => {
    vi.mocked(deleteKbArticle).mockResolvedValue(undefined)
    renderPage()
    await screen.findByRole('row', { name: /Invoices/ })

    fireEvent.click(within(rowOf('Invoices')).getByRole('button', { name: 'Delete' }))
    fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Delete' }))

    await waitFor(() => expect(deleteKbArticle).toHaveBeenCalledWith('a2'))
  })

  it('filters by status', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Invoices/ })

    fireEvent.change(screen.getByLabelText('Filter by status'), { target: { value: 'published' } })

    await waitFor(() =>
      expect(listKbArticles).toHaveBeenLastCalledWith(expect.objectContaining({ status: 'published' }), expect.anything()),
    )
  })

  it('hides the writing actions from a user without kb.manage', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue(agent)
    vi.mocked(listKbArticles).mockResolvedValue(page([live]))
    renderPage()

    expect(await screen.findByRole('row', { name: /Invoices/ })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'New article' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Unpublish' })).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Filter by status')).not.toBeInTheDocument()
  })

  it('manages categories in the Categories tab', async () => {
    vi.mocked(createKbCategory).mockResolvedValue({ ...billing, id: 'c2', name: 'Shipping' })
    renderPage()
    fireEvent.click(await screen.findByRole('tab', { name: 'Categories' }))
    expect(await screen.findByRole('row', { name: /Billing/ })).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'New category' }))
    const dialog = await screen.findByRole('dialog')
    fireEvent.change(within(dialog).getByLabelText('Name (English)'), { target: { value: 'Shipping' } })
    fireEvent.change(within(dialog).getByLabelText('Name (Arabic)'), { target: { value: 'الشحن' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(createKbCategory).toHaveBeenCalledWith({ nameEn: 'Shipping', nameAr: 'الشحن' }))
  })

  it('deletes an empty category', async () => {
    vi.mocked(listKbCategories).mockResolvedValue([{ ...billing, articleCount: 0 }])
    vi.mocked(deleteKbCategory).mockResolvedValue(undefined)
    renderPage()
    fireEvent.click(await screen.findByRole('tab', { name: 'Categories' }))
    await screen.findByRole('row', { name: /Billing/ })

    fireEvent.click(within(rowOf('Billing')).getByRole('button', { name: 'Delete' }))
    fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Delete' }))

    await waitFor(() => expect(deleteKbCategory).toHaveBeenCalledWith('c1'))
  })
})

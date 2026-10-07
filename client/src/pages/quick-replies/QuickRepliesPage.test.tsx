import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { ApiError } from '@/api/errors'
import {
  createQuickReply,
  deleteQuickReply,
  listQuickReplies,
  updateQuickReply,
  type QuickReply,
} from '@/api/quick-replies'
import { createQueryClient } from '@/app/query-client'
import { permissions } from '@/auth/permissions'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { QuickRepliesPage } from './QuickRepliesPage'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))
vi.mock('@/api/quick-replies', () => ({
  listQuickReplies: vi.fn(),
  createQuickReply: vi.fn(),
  updateQuickReply: vi.fn(),
  deleteQuickReply: vi.fn(),
}))

const agent: CurrentUser = {
  id: '2',
  email: 'agent@crm.local',
  fullName: 'Sara Agent',
  roles: ['Agent'],
  permissions: [permissions.ticketsView, permissions.ticketsManage],
}
const supervisor: CurrentUser = { ...agent, id: '3', roles: ['Supervisor'], permissions: [...agent.permissions, permissions.quickRepliesManageShared] }

const base = { ownerId: '2', ownerName: 'Sara Agent', createdAt: '2026-10-01T08:00:00Z', updatedAt: '2026-10-01T08:00:00Z' }
const greeting: QuickReply = { ...base, id: 'q1', title: 'Greeting', shortcut: '/hi', body: 'Hello {{customer.name}}', isShared: false, isMine: true }
const policy: QuickReply = { ...base, id: 'q2', title: 'Refund policy', shortcut: null, body: 'Refunds take 5 days.', isShared: true, isMine: false, ownerId: '3', ownerName: 'Team Lead' }

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <QuickRepliesPage />
      <ApiErrorToaster />
    </QueryClientProvider>,
  )
}

describe('QuickRepliesPage', () => {
  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(agent)
    vi.mocked(listQuickReplies).mockReset().mockResolvedValue([greeting, policy])
    vi.mocked(createQuickReply).mockReset().mockResolvedValue(greeting)
    vi.mocked(updateQuickReply).mockReset().mockResolvedValue(greeting)
    vi.mocked(deleteQuickReply).mockReset().mockResolvedValue(undefined)
  })

  it('lists my replies and the shared ones', async () => {
    renderPage()

    expect(screen.getByRole('heading', { level: 1, name: 'Quick replies' })).toBeInTheDocument()
    const mine = await screen.findByRole('row', { name: /Greeting/ })
    expect(within(mine).getByText('/hi')).toBeInTheDocument()
    expect(within(screen.getByRole('row', { name: /Refund policy/ })).getByText('Shared')).toBeInTheDocument()
  })

  it('shows only the title, on one line, in the Title column; the text has its own Preview column', async () => {
    renderPage()

    const row = await screen.findByRole('row', { name: /Greeting/ })
    const titleCell = within(row).getAllByRole('cell')[0]
    expect(titleCell).toHaveTextContent(/^Greeting$/)
    expect(within(titleCell).getByText('Greeting')).toHaveClass('truncate')
    expect(within(titleCell).getByText('Greeting')).toHaveAttribute('title', 'Greeting')
    expect(screen.getByRole('columnheader', { name: 'Preview' })).toBeInTheDocument()
    expect(within(row).getAllByRole('cell')[1]).toHaveTextContent('Hello')
  })

  it('creates a personal reply with placeholders', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Greeting/ })

    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Thanks' } })
    fireEvent.change(screen.getByLabelText('Shortcut'), { target: { value: '/thanks' } })
    fireEvent.change(screen.getByLabelText('Reply text'), { target: { value: 'Thanks {{customer.name}}!' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save quick reply' }))

    await waitFor(() =>
      expect(createQuickReply).toHaveBeenCalledWith({ title: 'Thanks', shortcut: '/thanks', body: 'Thanks {{customer.name}}!', isShared: false }),
    )
    await waitFor(() => expect(listQuickReplies).toHaveBeenCalledTimes(2))
  })

  it('requires a title and a text before calling the API', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Greeting/ })

    fireEvent.click(screen.getByRole('button', { name: 'Save quick reply' }))

    expect(await screen.findByText('Enter a title.')).toBeInTheDocument()
    expect(screen.getByText('Enter the reply text.')).toBeInTheDocument()
    expect(createQuickReply).not.toHaveBeenCalled()
  })

  it('offers "Shared" only to users who may manage shared replies', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Greeting/ })
    await waitFor(() => expect(getCurrentUser).toHaveBeenCalled())
    expect(screen.queryByLabelText('Share with everybody')).not.toBeInTheDocument()
  })

  it('lets a supervisor share a reply', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue(supervisor)
    renderPage()
    await screen.findByRole('row', { name: /Greeting/ })

    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Policy' } })
    fireEvent.change(screen.getByLabelText('Reply text'), { target: { value: 'Text' } })
    fireEvent.click(await screen.findByLabelText('Share with everybody'))
    fireEvent.click(screen.getByRole('button', { name: 'Save quick reply' }))

    await waitFor(() => expect(createQuickReply).toHaveBeenCalledWith(expect.objectContaining({ isShared: true })))
  })

  it('edits a reply', async () => {
    renderPage()
    const row = await screen.findByRole('row', { name: /Greeting/ })

    fireEvent.click(within(row).getByRole('button', { name: 'Edit' }))
    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Hello' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save quick reply' }))

    await waitFor(() => expect(updateQuickReply).toHaveBeenCalledWith('q1', expect.objectContaining({ title: 'Hello' })))
  })

  it('deletes a reply', async () => {
    renderPage()
    const row = await screen.findByRole('row', { name: /Greeting/ })

    fireEvent.click(within(row).getByRole('button', { name: 'Delete' }))

    await waitFor(() => expect(deleteQuickReply).toHaveBeenCalledWith('q1'))
  })

  it('keeps the form open when the server refuses to change a shared reply (403)', async () => {
    vi.mocked(updateQuickReply).mockRejectedValue(
      new ApiError('PUT failed with status 403', 403, { status: 403, title: 'You may not create or change shared quick replies.' }),
    )
    renderPage()
    const row = await screen.findByRole('row', { name: /Refund policy/ })

    fireEvent.click(within(row).getByRole('button', { name: 'Edit' }))
    fireEvent.click(screen.getByRole('button', { name: 'Save quick reply' }))

    await waitFor(() => expect(updateQuickReply).toHaveBeenCalledWith('q2', expect.anything()))
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeInTheDocument() // still editing; the API client shows the error toast
  })
  it('describes the page without raw placeholders', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Greeting/ })

    expect(screen.getByText(/Ready-made replies you can drop into any ticket/)).toBeInTheDocument()
    expect(screen.queryByText(/{{ticket.number}}/)).not.toBeInTheDocument()
  })

  it('shows placeholders in the list as badges', async () => {
    renderPage()
    const row = await screen.findByRole('row', { name: /Greeting/ })

    expect(within(row).getByText('Customer name')).toBeInTheDocument()
    expect(within(row).queryByText(/{{/)).not.toBeInTheDocument()
  })

  it('inserts a token at the caret and keeps focus after it', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Greeting/ })
    const box = screen.getByLabelText('Reply text') as HTMLTextAreaElement
    fireEvent.change(box, { target: { value: 'Hello , welcome' } })
    box.setSelectionRange(6, 6)

    fireEvent.click(screen.getByRole('button', { name: 'Insert Customer name' }))

    expect(box.value).toBe('Hello {{customer.name}}, welcome')
    await waitFor(() => expect(box).toHaveFocus())
    expect(box.selectionStart).toBe(6 + '{{customer.name}}'.length)
    expect(box.selectionEnd).toBe(box.selectionStart)
  })

  it('has an insert chip for each placeholder and replaces a selection', () => {
    renderPage()
    const box = screen.getByLabelText('Reply text') as HTMLTextAreaElement
    fireEvent.change(box, { target: { value: 'Re: XXX' } })
    box.setSelectionRange(4, 7)

    fireEvent.click(screen.getByRole('button', { name: 'Insert Ticket subject' }))

    expect(box.value).toBe('Re: {{ticket.subject}}')
    for (const name of ['Insert Ticket number', 'Insert Agent name']) {
      expect(screen.getByRole('button', { name })).toBeInTheDocument()
    }
  })

  it('previews the reply with sample values and the current user name', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Greeting/ })
    await waitFor(() => expect(getCurrentUser).toHaveBeenCalled())

    fireEvent.change(screen.getByLabelText('Reply text'), { target: { value: 'Hi {{customer.name}}, {{ticket.number}} - {{agent.name}}' } })

    await waitFor(() => expect(screen.getByTestId('quick-reply-preview')).toHaveTextContent('Hi Ahmed Ali, TKT-000123 - Sara Agent'))
  })

  it('filters the list by search and says when nothing matches', async () => {
    renderPage()
    await screen.findByRole('row', { name: /Greeting/ })

    fireEvent.change(screen.getByLabelText('Search by title, shortcut or text'), { target: { value: 'refund' } })
    expect(screen.queryByRole('row', { name: /Greeting/ })).not.toBeInTheDocument()
    expect(screen.getByRole('row', { name: /Refund policy/ })).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText('Search by title, shortcut or text'), { target: { value: 'zzz' } })
    expect(screen.getByText('No quick replies match your search.')).toBeInTheDocument()
  })

  it('shows a friendly empty state', async () => {
    vi.mocked(listQuickReplies).mockResolvedValue([])
    renderPage()

    expect(await screen.findByText('No quick replies yet')).toBeInTheDocument()
  })
})

import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { listQuickReplies, renderQuickReply, type QuickReply } from '@/api/quick-replies'
import { createQueryClient } from '@/app/query-client'
import { QuickReplyPicker } from './QuickReplyPicker'

vi.mock('@/api/quick-replies', () => ({ listQuickReplies: vi.fn(), renderQuickReply: vi.fn() }))

function reply(id: string, title: string, shortcut: string | null): QuickReply {
  return {
    id,
    title,
    shortcut,
    body: 'Hello {{customer.name}}',
    isShared: false,
    ownerId: '1',
    ownerName: 'Sara Agent',
    isMine: true,
    createdAt: '2026-10-01T08:00:00Z',
    updatedAt: '2026-10-01T08:00:00Z',
  }
}

function renderPicker(onInsert = vi.fn()) {
  render(
    <QueryClientProvider client={createQueryClient()}>
      <QuickReplyPicker ticketId="t1" onInsert={onInsert} />
    </QueryClientProvider>,
  )
  return onInsert
}

describe('QuickReplyPicker', () => {
  beforeEach(() => {
    vi.mocked(listQuickReplies)
      .mockReset()
      .mockResolvedValue([reply('q1', 'Greeting', '/hi'), reply('q2', 'Refund policy', '/refund')])
    vi.mocked(renderQuickReply).mockReset().mockResolvedValue({ text: 'Hello Nour Trading' })
  })

  it('lists the quick replies when opened, without loading them before', async () => {
    renderPicker()
    expect(listQuickReplies).not.toHaveBeenCalled()

    fireEvent.click(screen.getByRole('button', { name: 'Quick replies' }))

    expect(await screen.findByRole('button', { name: /Greeting/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Refund policy/ })).toBeInTheDocument()
  })

  it('inserts the rendered text of the chosen reply', async () => {
    const onInsert = renderPicker()
    fireEvent.click(screen.getByRole('button', { name: 'Quick replies' }))

    fireEvent.click(await screen.findByRole('button', { name: /Greeting/ }))

    await waitFor(() => expect(renderQuickReply).toHaveBeenCalledWith('q1', 't1'))
    await waitFor(() => expect(onInsert).toHaveBeenCalledWith('Hello Nour Trading'))
  })

  it('searches by title or shortcut', async () => {
    renderPicker()
    fireEvent.click(screen.getByRole('button', { name: 'Quick replies' }))
    await screen.findByRole('button', { name: /Greeting/ })

    fireEvent.change(screen.getByLabelText('Search quick replies'), { target: { value: '/refund' } })

    await waitFor(() => expect(listQuickReplies).toHaveBeenLastCalledWith('/refund', expect.anything()))
  })

  it('says so when nothing matches', async () => {
    vi.mocked(listQuickReplies).mockResolvedValue([])
    renderPicker()

    fireEvent.click(screen.getByRole('button', { name: 'Quick replies' }))

    expect(await screen.findByText('No quick replies found.')).toBeInTheDocument()
  })
})

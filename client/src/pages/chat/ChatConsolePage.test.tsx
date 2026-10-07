import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as chatApi from '@/api/chat'
import { createQueryClient } from '@/app/query-client'
import { ChatConsolePage } from './ChatConsolePage'

vi.mock('@/api/chat', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/chat')>()),
  connectChatHub: vi.fn(),
  listChatSessions: vi.fn(),
  listChatMessages: vi.fn(),
}))

const waitingChat: chatApi.ChatSession = {
  id: 's1', visitorName: 'Nour Ali', visitorEmail: 'n@x.example', status: 'waiting',
  agentId: null, agentName: null, startedAt: '2026-10-06T08:00:00Z', endedAt: null, ticketNumber: null,
}
const activeChat = { ...waitingChat, status: 'active' as const, agentId: 'a1', agentName: 'Sara' }

let handlers: chatApi.ChatHandlers
let accepted = false
const hub = {
  accept: vi.fn(async () => {
    accepted = true
    return activeChat
  }),
  send: vi.fn(async () => ({}) as chatApi.ChatMessage),
  end: vi.fn(async () => ({ ...activeChat, status: 'ended' as const })),
  stop: vi.fn(async () => {}),
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <ChatConsolePage />
    </QueryClientProvider>,
  )
}

describe('Agent chat console', () => {
  beforeEach(() => {
    accepted = false
    vi.mocked(chatApi.connectChatHub).mockImplementation(async (options) => {
      handlers = options.handlers
      return hub
    })
    vi.mocked(chatApi.listChatSessions).mockImplementation(async (status) =>
      status === 'waiting' ? (accepted ? [] : [waitingChat]) : accepted ? [activeChat] : [],
    )
    vi.mocked(chatApi.listChatMessages).mockResolvedValue([
      { id: 'm0', sessionId: 's1', sender: 'visitor', senderName: 'Nour Ali', body: 'Hello', sentAt: '2026-10-06T08:00:00Z' },
    ])
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  it('lists waiting chats and accepts one', async () => {
    renderPage()

    fireEvent.click(await screen.findByRole('button', { name: 'Accept' }))

    await waitFor(() => expect(hub.accept).toHaveBeenCalledWith('s1'))
    expect(await screen.findByText('Hello')).toBeInTheDocument()
  })

  it('sends a message and shows incoming ones live', async () => {
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Accept' }))
    await screen.findByText('Hello')

    handlers.onMessage?.({ id: 'm1', sessionId: 's1', sender: 'visitor', senderName: 'Nour Ali', body: 'Where is my order?', sentAt: '2026-10-06T08:01:00Z' })
    expect(await screen.findByText('Where is my order?')).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText('Your message'), { target: { value: 'Checking now' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))
    await waitFor(() => expect(hub.send).toHaveBeenCalledWith('s1', 'Checking now'))
  })

  it('ends the chat and tells the agent the transcript was saved', async () => {
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Accept' }))
    await screen.findByText('Hello')

    fireEvent.click(screen.getByRole('button', { name: 'End chat' }))
    await waitFor(() => expect(hub.end).toHaveBeenCalledWith('s1'))
    handlers.onChatEnded?.({ ...activeChat, status: 'ended' })

    expect(await screen.findByText('This chat has ended. The transcript was saved as a ticket.')).toBeInTheDocument()
  })
})

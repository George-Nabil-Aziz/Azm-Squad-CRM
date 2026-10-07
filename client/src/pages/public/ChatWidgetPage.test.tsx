import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as chatApi from '@/api/chat'
import { ApiError } from '@/api/errors'
import { createQueryClient } from '@/app/query-client'
import { ChatWidgetPage } from './ChatWidgetPage'

vi.mock('@/api/chat', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/chat')>()),
  connectChatHub: vi.fn(),
  getChatAvailability: vi.fn(),
  startChat: vi.fn(),
  submitOfflineChat: vi.fn(),
}))
vi.mock('@/api/web-forms', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/api/web-forms')>()),
  getWebFormConfig: vi.fn(async () => ({ captchaRequired: false, captchaSiteKey: null })),
}))

const hub = {
  accept: vi.fn(),
  send: vi.fn(async () => ({}) as chatApi.ChatMessage),
  end: vi.fn(),
  stop: vi.fn(async () => {}),
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <ChatWidgetPage />
    </QueryClientProvider>,
  )
}

describe('Live chat widget', () => {
  beforeEach(() => {
    vi.mocked(chatApi.connectChatHub).mockResolvedValue(hub)
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  it('starts a chat when an agent is online and sends messages', async () => {
    vi.mocked(chatApi.getChatAvailability).mockResolvedValue({ available: true })
    vi.mocked(chatApi.startChat).mockResolvedValue({
      session: { id: 's1', visitorName: 'Nour', visitorEmail: 'n@x.example', status: 'waiting', agentId: null, agentName: null, startedAt: '2026-10-06T08:00:00Z', endedAt: null, ticketNumber: null },
      visitorToken: 'tok',
    })
    renderPage()

    fireEvent.change(await screen.findByLabelText('Your name'), { target: { value: 'Nour' } })
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'n@x.example' } })
    fireEvent.change(screen.getByLabelText('How can we help?'), { target: { value: 'Hi there' } })
    fireEvent.click(screen.getByRole('button', { name: 'Start chat' }))

    expect(await screen.findByText('Hi there')).toBeInTheDocument()
    expect(chatApi.connectChatHub).toHaveBeenCalledWith(expect.objectContaining({ visitor: { sessionId: 's1', token: 'tok' } }))
    fireEvent.change(screen.getByLabelText('Your message'), { target: { value: 'Anyone there?' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))
    await waitFor(() => expect(hub.send).toHaveBeenCalledWith('s1', 'Anyone there?'))
  })

  it('shows the offline form when no agent is online', async () => {
    vi.mocked(chatApi.getChatAvailability).mockResolvedValue({ available: false })
    renderPage()

    expect(await screen.findByText('No agent is online right now. Leave a message and we will reply by email.')).toBeInTheDocument()
    expect(screen.getByLabelText('Subject')).toBeInTheDocument()
  })

  it('falls back to the offline form when starting is refused with 409', async () => {
    vi.mocked(chatApi.getChatAvailability).mockResolvedValue({ available: true })
    vi.mocked(chatApi.startChat).mockRejectedValue(new ApiError('conflict', 409))
    renderPage()

    fireEvent.change(await screen.findByLabelText('Your name'), { target: { value: 'Nour' } })
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'n@x.example' } })
    fireEvent.click(screen.getByRole('button', { name: 'Start chat' }))

    expect(await screen.findByLabelText('Subject')).toBeInTheDocument()
  })
})

import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createQueryClient } from '@/app/query-client'
import { getChatbotStatus, sendChatbotMessage, type ChatbotReply } from '@/api/portal-chatbot'
import { PortalChatPage } from './PortalChatPage'

vi.mock('@/api/portal-chatbot', () => ({ getChatbotStatus: vi.fn(), sendChatbotMessage: vi.fn() }))

const answered: ChatbotReply = {
  answer: 'Use the reset link on the sign-in page.',
  language: 'en',
  sources: [{ id: 'a1', title: 'Reset your password' }],
  outcome: 'answered',
  offerAgent: false,
  signInRequired: false,
  ticket: null,
}

function renderPage() {
  render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter>
        <PortalChatPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

async function ask(text: string) {
  fireEvent.change(await screen.findByLabelText('Your message'), { target: { value: text } })
  fireEvent.click(screen.getByRole('button', { name: 'Send' }))
}

describe('Portal chatbot', () => {
  beforeEach(() => {
    vi.mocked(getChatbotStatus).mockReset().mockResolvedValue({ enabled: true })
    vi.mocked(sendChatbotMessage).mockReset().mockResolvedValue(answered)
  })

  it('sends the question and shows the answer with a link to the cited article', async () => {
    renderPage()

    await ask('How do I reset my password?')

    expect(await screen.findByText('Use the reset link on the sign-in page.')).toBeInTheDocument()
    expect(sendChatbotMessage).toHaveBeenCalledWith([{ role: 'user', content: 'How do I reset my password?' }], false)
    expect(screen.getByRole('link', { name: 'Reset your password' })).toHaveAttribute('href', '/portal/kb/articles/a1')
    expect((screen.getByLabelText('Your message') as HTMLInputElement).value).toBe('')
  })

  it('sends the whole conversation with the next question', async () => {
    renderPage()
    await ask('First question')
    await screen.findByText(answered.answer)

    await ask('Second question')

    await waitFor(() => expect(sendChatbotMessage).toHaveBeenCalledTimes(2))
    expect(vi.mocked(sendChatbotMessage).mock.calls[1][0]).toEqual([
      { role: 'user', content: 'First question' },
      { role: 'assistant', content: answered.answer },
      { role: 'user', content: 'Second question' },
    ])
  })

  it('offers an agent when it does not know, and the button asks for the hand-off', async () => {
    vi.mocked(sendChatbotMessage).mockResolvedValueOnce({
      ...answered, answer: 'I could not find this in our help articles.', sources: [], outcome: 'unknown', offerAgent: true,
    })
    vi.mocked(sendChatbotMessage).mockResolvedValueOnce({
      ...answered, answer: 'Request TKT-000009 was created.', sources: [], outcome: 'handoff', ticket: { id: 't9', number: 'TKT-000009' },
    })
    renderPage()
    await ask('Odd question')

    fireEvent.click(await screen.findByRole('button', { name: 'Talk to an agent' }))

    expect(await screen.findByText('Request TKT-000009 was created.')).toBeInTheDocument()
    expect(vi.mocked(sendChatbotMessage).mock.calls[1][1]).toBe(true)
    expect(screen.getByRole('link', { name: 'TKT-000009' })).toHaveAttribute('href', '/portal/tickets/t9')
    expect(screen.queryByRole('button', { name: 'Talk to an agent' })).not.toBeInTheDocument()
  })

  it('asks a visitor to sign in when a hand-off needs an account', async () => {
    vi.mocked(sendChatbotMessage).mockResolvedValue({
      ...answered, answer: 'Please sign in first.', sources: [], outcome: 'handoff', signInRequired: true,
    })
    renderPage()

    await ask('I want a human')

    expect(await screen.findByText('Please sign in first.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Sign in' })).toHaveAttribute('href', '/portal/login')
  })

  it('writes Arabic messages right to left', async () => {
    vi.mocked(sendChatbotMessage).mockResolvedValue({ ...answered, answer: 'استخدم رابط إعادة التعيين.', language: 'ar' })
    renderPage()

    await ask('كيف أعيد تعيين كلمة المرور؟')

    const reply = await screen.findByText('استخدم رابط إعادة التعيين.')
    expect(reply).toHaveAttribute('dir', 'rtl')
    expect(within(screen.getByRole('log')).getByText('كيف أعيد تعيين كلمة المرور؟')).toHaveAttribute('dir', 'auto')
  })

  it('says the chat is not available when the server has no AI key', async () => {
    vi.mocked(getChatbotStatus).mockResolvedValue({ enabled: false })
    renderPage()

    expect(await screen.findByText('The chat is not available right now.')).toBeInTheDocument()
    expect(screen.queryByLabelText('Your message')).not.toBeInTheDocument()
  })

  it('keeps the conversation when sending fails', async () => {
    vi.mocked(sendChatbotMessage).mockRejectedValue(new Error('network'))
    renderPage()

    await ask('Hello there')

    await waitFor(() => expect(sendChatbotMessage).toHaveBeenCalled())
    expect(await screen.findByText('Hello there')).toBeInTheDocument()
    expect(await screen.findByText('The message could not be sent. Please try again.')).toBeInTheDocument()
  })
})

import { afterEach, describe, expect, it, vi } from 'vitest'
import { getChatbotStatus, sendChatbotMessage } from './portal-chatbot'

function fakeFetch(body: unknown) {
  const fetchMock = vi.fn().mockResolvedValue(
    new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } }),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('portal chatbot API', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('reads the status with GET /api/portal/chatbot/status', async () => {
    const fetchMock = fakeFetch({ enabled: true })

    expect(await getChatbotStatus()).toEqual({ enabled: true })
    expect(fetchMock.mock.calls[0][0]).toBe('/api/portal/chatbot/status')
  })

  it('posts the transcript and the hand-off flag', async () => {
    const fetchMock = fakeFetch({ answer: 'Hi' })

    await sendChatbotMessage([{ role: 'user', content: 'Hello' }], true)

    expect(fetchMock.mock.calls[0][0]).toBe('/api/portal/chatbot/messages')
    expect(JSON.parse(String((fetchMock.mock.calls[0][1] as RequestInit).body))).toEqual({
      messages: [{ role: 'user', content: 'Hello' }],
      handoff: true,
    })
  })
})

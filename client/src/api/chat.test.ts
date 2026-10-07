import { afterEach, describe, expect, it, vi } from 'vitest'
import { getChatAvailability, listChatMessages, listChatSessions, startChat, submitOfflineChat } from './chat'

function fakeFetch(body: unknown = {}, status = 200) {
  const fetchMock = vi.fn().mockResolvedValue(
    new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

const call = (fetchMock: ReturnType<typeof fakeFetch>) => {
  const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit]
  return [path, init.method] as const
}

describe('chat API', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('reads availability', async () => {
    const fetchMock = fakeFetch({ available: true })
    expect((await getChatAvailability()).available).toBe(true)
    expect(call(fetchMock)).toEqual(['/api/public/chat/availability', 'GET'])
  })

  it('starts a chat with POST /api/public/chat/sessions', async () => {
    const fetchMock = fakeFetch({ session: { id: 's1' }, visitorToken: 'tok' }, 201)
    const started = await startChat({ name: 'Nour', email: 'n@x.example', message: 'Hi' })
    expect(started.visitorToken).toBe('tok')
    expect(call(fetchMock)).toEqual(['/api/public/chat/sessions', 'POST'])
  })

  it('sends the offline form to POST /api/public/chat/offline', async () => {
    const fetchMock = fakeFetch({ number: 'TKT-000001' }, 201)
    await submitOfflineChat({ name: 'N', email: 'n@x.example', subject: 'S', message: 'M', captchaToken: '', website: '' })
    expect(call(fetchMock)).toEqual(['/api/public/chat/offline', 'POST'])
  })

  it('lists the queue, own chats and messages for agents', async () => {
    const waiting = fakeFetch([])
    await listChatSessions('waiting')
    expect(call(waiting)).toEqual(['/api/chat-sessions?status=waiting', 'GET'])
    const messages = fakeFetch([])
    await listChatMessages('s1')
    expect(call(messages)).toEqual(['/api/chat-sessions/s1/messages', 'GET'])
  })
})

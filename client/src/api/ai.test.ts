import { afterEach, describe, expect, it, vi } from 'vitest'
import { generateReplyDraft, generateTicketSummary, getAiStatus, getTicketSummary } from './ai'

function fakeFetch(response: Response) {
  const fetchMock = vi.fn().mockResolvedValue(response)
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

const json = (body: unknown) =>
  new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })

describe('AI API', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('reads whether AI is configured with GET /api/ai/status', async () => {
    const fetchMock = fakeFetch(json({ enabled: true }))

    expect(await getAiStatus()).toEqual({ enabled: true })
    expect(fetchMock.mock.calls[0][0]).toBe('/api/ai/status')
  })

  it('reads the saved summary with GET /api/tickets/{id}/ai-summary', async () => {
    const fetchMock = fakeFetch(json({ text: '- A', language: 'en', generatedAt: '2026-10-01T08:00:00Z' }))

    expect((await getTicketSummary('t 1')).text).toBe('- A')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/tickets/t%201/ai-summary')
  })

  it('generates a summary with POST /api/tickets/{id}/ai-summary', async () => {
    const fetchMock = fakeFetch(json({ text: '- B', language: 'ar', generatedAt: '2026-10-01T09:00:00Z' }))

    expect((await generateTicketSummary('t1')).language).toBe('ar')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/tickets/t1/ai-summary')
    expect((fetchMock.mock.calls[0][1] as RequestInit).method).toBe('POST')
  })

  it('asks for a reply draft with POST /api/tickets/{id}/ai-reply-draft', async () => {
    const fetchMock = fakeFetch(json({ draft: 'Hello', language: 'en', articles: [{ id: 'a1', title: 'Reset' }] }))

    const draft = await generateReplyDraft('t1')

    expect(draft.articles[0].title).toBe('Reset')
    expect(fetchMock.mock.calls[0][0]).toBe('/api/tickets/t1/ai-reply-draft')
    expect((fetchMock.mock.calls[0][1] as RequestInit).method).toBe('POST')
  })
})

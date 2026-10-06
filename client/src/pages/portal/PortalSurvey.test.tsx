import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from '@/App'
import { clearPortalSession, savePortalSession } from '@/auth/portal-session'
import { clearSession } from '@/auth/session'

function json(status: number, body: unknown, type = 'application/json') {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': type } })
}

const open = { ticketNumber: 'TKT-000007', subject: 'Printer is broken', state: 'open', rating: null, comment: null, expiresAt: '2026-10-08T08:00:00Z' }
let survey: Record<string, unknown> = open
let submitResponse: () => Response
const posts: unknown[] = []

function stubApi() {
  const fetchMock = vi.fn(async (path: string, init?: RequestInit) => {
    if (path === '/api/portal/surveys/tok' && init?.method === 'POST') {
      posts.push(JSON.parse(String(init.body)))
      return submitResponse()
    }
    if (path === '/api/portal/surveys/tok') return json(200, survey)
    if (path === '/api/portal/tickets/t1') {
      return json(200, {
        id: 't1', number: 'TKT-000007', subject: 'Printer is broken', description: null, status: 'resolved', categoryName: null,
        createdAt: '2026-10-01T08:00:00Z', updatedAt: '2026-10-02T08:00:00Z', canReply: false, canReopen: false,
      })
    }
    if (path === '/api/portal/tickets/t1/feedback' && init?.method === 'POST') {
      posts.push(JSON.parse(String(init.body)))
      return submitResponse()
    }
    if (path === '/api/portal/tickets/t1/feedback') return json(200, survey)
    if (path.endsWith('/messages') || path.endsWith('/history')) return json(200, [])
    return json(404, { status: 404 }, 'application/problem+json')
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function renderAt(path: string) {
  window.history.replaceState(null, '', path)
  return render(<App />)
}

describe('Portal satisfaction survey', () => {
  beforeEach(() => {
    survey = open
    posts.length = 0
    submitResponse = () => json(200, { ...open, state: 'answered', rating: 4, comment: 'Fast help' })
    clearSession()
    clearPortalSession()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    clearPortalSession()
    localStorage.clear()
  })

  it('shows the survey of the emailed link (no sign-in) and saves a rating with a comment', async () => {
    stubApi()
    renderAt('/portal/survey/tok')

    expect(await screen.findByText(/Printer is broken/)).toBeInTheDocument()
    fireEvent.click(screen.getByRole('radio', { name: '4 stars' }))
    fireEvent.change(screen.getByLabelText('Comment (optional)'), { target: { value: 'Fast help' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send rating' }))

    expect(await screen.findByText('Thank you for your feedback.')).toBeInTheDocument()
    expect(screen.getByText('Your rating: 4 out of 5.')).toBeInTheDocument()
    expect(posts).toEqual([{ rating: 4, comment: 'Fast help' }])
  })

  it('asks for a rating before sending', async () => {
    stubApi()
    renderAt('/portal/survey/tok')

    fireEvent.click(await screen.findByRole('button', { name: 'Send rating' }))

    expect(await screen.findByText('Choose 1 to 5 stars.')).toBeInTheDocument()
    expect(posts).toHaveLength(0)
  })

  it('shows the server message when the rating is refused (already rated)', async () => {
    submitResponse = () => json(400, { status: 400, errors: { rating: ['You already rated this request.'] } }, 'application/problem+json')
    stubApi()
    renderAt('/portal/survey/tok')
    fireEvent.click(await screen.findByRole('radio', { name: '5 stars' }))

    fireEvent.click(screen.getByRole('button', { name: 'Send rating' }))

    expect(await screen.findByText('You already rated this request.')).toBeInTheDocument()
  })

  it('tells the customer when the link expired', async () => {
    survey = { ...open, state: 'expired' }
    stubApi()
    renderAt('/portal/survey/tok')

    expect(await screen.findByText('This survey link has expired.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Send rating' })).not.toBeInTheDocument()
  })

  it('shows the saved answer of an answered survey', async () => {
    survey = { ...open, state: 'answered', rating: 5, comment: 'Great' }
    stubApi()
    renderAt('/portal/survey/tok')

    expect(await screen.findByText('Your rating: 5 out of 5.')).toBeInTheDocument()
    expect(screen.getByText('Great')).toBeInTheDocument()
  })

  it('says so for an unknown link', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => json(404, { status: 404 }, 'application/problem+json')))
    renderAt('/portal/survey/zzz')

    expect(await screen.findByText('This survey was not found.')).toBeInTheDocument()
  })

  it('offers the survey inside the portal on a resolved request', async () => {
    savePortalSession('portal-token', new Date(Date.now() + 3_600_000).toISOString(), { id: 'c1', name: 'Nour', email: 'n@x.example' })
    stubApi()
    renderAt('/portal/tickets/t1')

    fireEvent.click(await screen.findByRole('radio', { name: '4 stars' }))
    fireEvent.click(screen.getByRole('button', { name: 'Send rating' }))

    await waitFor(() => expect(posts).toEqual([{ rating: 4, comment: '' }]))
    expect(await screen.findByText('Thank you for your feedback.')).toBeInTheDocument()
  })

  it('shows nothing on a resolved request without a survey', async () => {
    survey = { ...open, state: 'none' }
    savePortalSession('portal-token', new Date(Date.now() + 3_600_000).toISOString(), { id: 'c1', name: 'Nour', email: 'n@x.example' })
    stubApi()
    renderAt('/portal/tickets/t1')

    await screen.findByRole('heading', { level: 1, name: 'Printer is broken' })
    expect(screen.queryByRole('button', { name: 'Send rating' })).not.toBeInTheDocument()
  })
})

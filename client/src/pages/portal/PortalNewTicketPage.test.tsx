import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from '@/App'
import { clearPortalSession, savePortalSession } from '@/auth/portal-session'

const inOneHour = () => new Date(Date.now() + 3_600_000).toISOString()

function json(status: number, body: unknown, type = 'application/json') {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': type } })
}

let submitResponse: () => Response

function stubApi() {
  const fetchMock = vi.fn(async (path: string, _init?: RequestInit) => {
    if (path === '/api/portal/ticket-categories') return json(200, [{ id: 'k1', name: 'Billing' }])
    if (path === '/api/portal/tickets') return submitResponse()
    return json(404, { status: 404 }, 'application/problem+json')
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function renderPage() {
  window.history.replaceState(null, '', '/portal/tickets/new')
  return render(<App />)
}

describe('Portal new request', () => {
  beforeEach(() => {
    savePortalSession('portal-token', inOneHour(), { id: 'c1', name: 'Nour', email: 'n@x.example' })
    submitResponse = () =>
      json(201, { id: 't1', number: 'TKT-000007', subject: 'Printer', status: 'new', createdAt: '2026-10-01T08:00:00Z', attachments: [] })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    clearPortalSession()
    localStorage.clear()
  })

  it('submits subject, description, category and a file as multipart and shows the ticket number', async () => {
    const fetchMock = stubApi()
    renderPage()
    await screen.findByRole('option', { name: 'Billing' })

    fireEvent.change(screen.getByLabelText('Subject'), { target: { value: 'Printer' } })
    fireEvent.change(screen.getByLabelText('Describe your request'), { target: { value: 'Blank pages' } })
    fireEvent.change(screen.getByLabelText('Category'), { target: { value: 'k1' } })
    const file = new File(['%PDF'], 'invoice.pdf', { type: 'application/pdf' })
    fireEvent.change(screen.getByLabelText('Attachments'), { target: { files: [file] } })
    fireEvent.click(screen.getByRole('button', { name: 'Submit request' }))

    expect(await screen.findByText('TKT-000007')).toBeInTheDocument()
    expect(screen.getByText('We also emailed you a confirmation.')).toBeInTheDocument()
    const call = fetchMock.mock.calls.find(([path]) => path === '/api/portal/tickets')!
    const form = (call[1] as RequestInit).body as FormData
    expect(form.get('subject')).toBe('Printer')
    expect(form.get('description')).toBe('Blank pages')
    expect(form.get('categoryId')).toBe('k1')
    expect((form.getAll('files')[0] as File).name).toBe('invoice.pdf')
    expect(((call[1] as RequestInit).headers as Record<string, string>).Authorization).toBe('Bearer portal-token')
  })

  it('asks for a subject before calling the API', async () => {
    const fetchMock = stubApi()
    renderPage()
    await screen.findByRole('option', { name: 'Billing' })

    fireEvent.click(screen.getByRole('button', { name: 'Submit request' }))

    expect(await screen.findByText('Enter a subject.')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([path]) => path === '/api/portal/tickets')).toBe(false)
  })

  it('shows the server messages of a 400 next to the fields', async () => {
    submitResponse = () =>
      json(400, { status: 400, errors: { subject: ['Server: subject missing.'], files: ['run.exe: type not allowed.'] } }, 'application/problem+json')
    stubApi()
    renderPage()
    await screen.findByRole('option', { name: 'Billing' })
    fireEvent.change(screen.getByLabelText('Subject'), { target: { value: 'X' } })

    fireEvent.click(screen.getByRole('button', { name: 'Submit request' }))

    expect(await screen.findByText('Server: subject missing.')).toBeInTheDocument()
    expect(screen.getByText('run.exe: type not allowed.')).toBeInTheDocument()
  })

  it('sends a signed-out visitor to the sign-in page', async () => {
    clearPortalSession()
    stubApi()
    renderPage()

    await waitFor(() => expect(window.location.pathname).toBe('/portal/login'))
  })
})

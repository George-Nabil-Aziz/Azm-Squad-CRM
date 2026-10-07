import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createQueryClient } from '@/app/query-client'
import { i18n } from '@/i18n/i18n'
import { ContactFormPage } from './ContactFormPage'

function json(status: number, body: unknown, type = 'application/json') {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': type } })
}

let submitResponse: () => Response

function stubApi() {
  const fetchMock = vi.fn(async (path: string, _init?: RequestInit) => {
    if (path === '/api/public/web-forms/config') return json(200, { captchaRequired: false, captchaSiteKey: null })
    if (path === '/api/public/web-forms') return submitResponse()
    return json(404, { status: 404 }, 'application/problem+json')
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <ContactFormPage />
    </QueryClientProvider>,
  )
}

function fill() {
  fireEvent.change(screen.getByLabelText('Your name'), { target: { value: 'Nour Ali' } })
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'nour@x.example' } })
  fireEvent.change(screen.getByLabelText('Subject'), { target: { value: 'Printer' } })
  fireEvent.change(screen.getByLabelText('Message'), { target: { value: 'Blank pages' } })
}

describe('Contact form (embedded web form)', () => {
  beforeEach(() => {
    submitResponse = () => json(201, { number: 'TKT-000007' })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('submits the fields and shows the ticket number', async () => {
    const fetchMock = stubApi()
    renderPage()

    fill()
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))

    expect(await screen.findByText('TKT-000007')).toBeInTheDocument()
    const call = fetchMock.mock.calls.find(([path]) => path === '/api/public/web-forms')!
    expect(JSON.parse(String((call[1] as RequestInit).body))).toMatchObject({
      name: 'Nour Ali',
      email: 'nour@x.example',
      subject: 'Printer',
      message: 'Blank pages',
    })
  })

  it('renders for an anonymous visitor as a centred card with the company name and a short intro', () => {
    stubApi()
    renderPage()

    const card = screen.getByRole('region', { name: 'Contact us' })
    expect(card).toHaveAttribute('data-slot', 'card')
    expect(screen.getByRole('heading', { level: 1, name: 'Contact us' })).toBeInTheDocument()
    expect(screen.getByText('Customer Support CRM')).toBeInTheDocument()
    expect(screen.getByRole('form', { name: 'Contact us' })).toBeInTheDocument()
  })

  it('does not overwrite the saved language when the address asks for one (the admin preview shares the browser)', async () => {
    stubApi()
    localStorage.setItem('crm.language', 'en')
    window.history.replaceState(null, '', '/embed/contact?lang=ar')
    renderPage()

    expect(await screen.findByRole('heading', { level: 1, name: 'تواصل معنا' })).toBeInTheDocument()
    expect(localStorage.getItem('crm.language')).toBe('en')
    window.history.replaceState(null, '', '/')
    await i18n.changeLanguage('en')
  })

  it('asks for the required fields before calling the API', async () => {
    const fetchMock = stubApi()
    renderPage()

    fireEvent.click(screen.getByRole('button', { name: 'Send' }))

    expect(await screen.findByText('Enter your name.')).toBeInTheDocument()
    expect(screen.getByText('Enter a valid email address.')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([path]) => path === '/api/public/web-forms')).toBe(false)
  })

  it('shows the server messages of a 400 next to the fields', async () => {
    submitResponse = () =>
      json(400, { status: 400, errors: { subject: ['Server: subject missing.'] } }, 'application/problem+json')
    stubApi()
    renderPage()

    fill()
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))

    expect(await screen.findByText('Server: subject missing.')).toBeInTheDocument()
  })

  it('tells the visitor to wait after a 429', async () => {
    submitResponse = () => json(429, { status: 429 }, 'application/problem+json')
    stubApi()
    renderPage()

    fill()
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))

    expect(await screen.findByText('Too many requests. Please wait a minute and try again.')).toBeInTheDocument()
  })
})

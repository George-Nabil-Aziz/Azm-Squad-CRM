import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from '@/App'
import { clearPortalSession, getPortalAccessToken } from '@/auth/portal-session'
import { getAccessToken } from '@/auth/session'

const inOneHour = () => new Date(Date.now() + 3_600_000).toISOString()

function json(status: number, body: unknown, type = 'application/json') {
  return new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': type } })
}

function stubApi(verify: () => Response) {
  const fetchMock = vi.fn(async (path: string, _init?: RequestInit) => {
    if (path === '/api/portal/auth/request-code') return new Response(null, { status: 204 })
    if (path === '/api/portal/auth/verify') return verify()
    return json(404, { status: 404 }, 'application/problem+json')
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function renderAt(path: string) {
  window.history.replaceState(null, '', path)
  return render(<App />)
}

async function requestCode(email = 'nour@customer.example') {
  fireEvent.change(await screen.findByLabelText('Email'), { target: { value: email } })
  fireEvent.click(screen.getByRole('button', { name: 'Send me a code' }))
  await screen.findByRole('form', { name: 'Enter the code' })
}

describe('Portal sign-in', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    clearPortalSession()
    localStorage.clear()
  })

  it('mails a code, then signs the customer in with the correct code', async () => {
    const fetchMock = stubApi(() =>
      json(200, { accessToken: 'portal-token', tokenType: 'Bearer', expiresAt: inOneHour(), customer: { id: 'c1', name: 'Nour', email: 'nour@customer.example' } }),
    )
    renderAt('/portal/login')

    await requestCode()
    expect(screen.getByText(/nour@customer.example/)).toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('Code'), { target: { value: '123456' } })
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    await waitFor(() => expect(getPortalAccessToken()).toBe('portal-token'))
    await waitFor(() => expect(window.location.pathname).toBe('/portal'))
    expect(JSON.parse(String(fetchMock.mock.calls[1][1]?.body))).toEqual({ email: 'nour@customer.example', code: '123456' })
    expect(getAccessToken()).toBeNull() // the staff session is untouched
  })

  it('shows an error for a wrong code and stays on the sign-in page', async () => {
    stubApi(() => json(401, { status: 401, title: 'bad' }, 'application/problem+json'))
    renderAt('/portal/login')
    await requestCode()

    fireEvent.change(screen.getByLabelText('Code'), { target: { value: '000000' } })
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('The code is not valid or has expired. Request a new code.')).toBeInTheDocument()
    expect(getPortalAccessToken()).toBeNull()
  })

  it('checks the email and the code format before calling the API', async () => {
    const fetchMock = stubApi(() => json(200, {}))
    renderAt('/portal/login')

    fireEvent.click(await screen.findByRole('button', { name: 'Send me a code' }))
    expect(await screen.findByText('Enter your email.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()

    await requestCode()
    fireEvent.change(screen.getByLabelText('Code'), { target: { value: '12' } })
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(await screen.findByText('Enter the 6-digit code.')).toBeInTheDocument()
  })

  it('sends a new code on request', async () => {
    const fetchMock = stubApi(() => json(200, {}))
    renderAt('/portal/login')
    await requestCode()

    fireEvent.click(screen.getByRole('button', { name: 'Send a new code' }))

    await waitFor(() => expect(fetchMock.mock.calls.filter(([p]) => p === '/api/portal/auth/request-code')).toHaveLength(2))
  })

  it('does not use the staff session: /portal pages stay open without a staff login', async () => {
    renderAt('/portal')

    expect(await screen.findByRole('heading', { level: 1, name: 'How can we help?' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/portal')
  })
})

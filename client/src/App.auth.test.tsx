import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { saveSession } from './auth/session'

const inOneHour = () => new Date(Date.now() + 60 * 60 * 1000).toISOString()

function json(status: number, body: unknown, contentType = 'application/json') {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': contentType } })
}

const me = { id: '1', email: 'admin@crm.local', fullName: 'System Administrator', roles: ['SuperAdmin'] }

/** Fake API: health is always ok; login accepts one password; /me needs the token login returned. */
function fakeApi() {
  return vi.fn(async (path: string, init?: RequestInit) => {
    if (path === '/api/health') return json(200, { status: 'ok' })
    if (path === '/api/auth/login') {
      const { password } = JSON.parse(String(init?.body)) as { password: string }
      return password === 'Admin#12345'
        ? json(200, { accessToken: 'good-token', tokenType: 'Bearer', expiresAt: inOneHour() })
        : json(401, { status: 401, title: 'Authentication failed.', correlationId: 'c-401' }, 'application/problem+json')
    }
    if (path === '/api/auth/me') {
      const auth = ((init?.headers ?? {}) as Record<string, string>).Authorization
      return auth === 'Bearer good-token' ? json(200, me) : json(401, { status: 401 }, 'application/problem+json')
    }
    return json(404, { status: 404 }, 'application/problem+json')
  })
}

function submitSignIn(email: string, password: string) {
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: email } })
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: password } })
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))
}

describe('App authentication', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the sign-in form when signed out', () => {
    vi.stubGlobal('fetch', fakeApi())
    render(<App />)

    expect(screen.getByRole('form', { name: 'Sign in' })).toBeInTheDocument()
  })

  it('signs in with valid credentials and shows the current user', async () => {
    vi.stubGlobal('fetch', fakeApi())
    render(<App />)

    submitSignIn('admin@crm.local', 'Admin#12345')

    expect(await screen.findByText('Signed in as System Administrator')).toBeInTheDocument()
    expect(screen.queryByRole('form', { name: 'Sign in' })).not.toBeInTheDocument()
  })

  it('shows an inline error and no toast for a wrong password', async () => {
    vi.stubGlobal('fetch', fakeApi())
    render(<App />)

    submitSignIn('admin@crm.local', 'wrong')

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.')
    expect(screen.queryByText('Something went wrong. Please try again.')).not.toBeInTheDocument()
  })

  it('returns to the sign-in form when the stored token is rejected', async () => {
    saveSession('expired-token', inOneHour())
    vi.stubGlobal('fetch', fakeApi())
    render(<App />)

    expect(await screen.findByRole('form', { name: 'Sign in' })).toBeInTheDocument()
  })

  it('signs out', async () => {
    vi.stubGlobal('fetch', fakeApi())
    render(<App />)
    submitSignIn('admin@crm.local', 'Admin#12345')
    await screen.findByText('Signed in as System Administrator')

    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }))

    expect(await screen.findByRole('form', { name: 'Sign in' })).toBeInTheDocument()
  })
})

import { fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { getAccessToken, saveSession } from './auth/session'
import { ADMIN_PASSWORD, callsTo, fakeApi, inOneHour, submitSignIn } from './test/fake-api'

const NAVIGATION_LABELS = ['Dashboard', 'Tickets', 'Customers', 'Knowledge base', 'Reports', 'Users']

function renderAt(path: string) {
  window.history.replaceState(null, '', path)
  return render(<App />)
}

function signedIn() {
  saveSession('good-token', inOneHour())
}

describe('App layout and routing', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('redirects / to /login when signed out', async () => {
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/')

    expect(await screen.findByRole('form', { name: 'Sign in' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/login')
  })

  it('lands on the dashboard with the sidebar navigation after a successful login', async () => {
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/')
    await screen.findByRole('form', { name: 'Sign in' })

    submitSignIn('admin@crm.local', ADMIN_PASSWORD)

    expect(await screen.findByRole('heading', { level: 1, name: 'Dashboard' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/')
    const navigation = screen.getByRole('navigation', { name: 'Main navigation' })
    const links = within(navigation).getAllByRole('link')
    expect(links.map((link) => link.textContent)).toEqual(NAVIGATION_LABELS)
    expect(within(navigation).getByRole('link', { name: 'Dashboard' })).toHaveAttribute('aria-current', 'page')
  })

  it('shows the signed-in user in the header', async () => {
    signedIn()
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/')

    expect(await screen.findByText('System Administrator')).toBeInTheDocument()
  })

  it('signs out: clears the token and returns to /login', async () => {
    signedIn()
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/')

    fireEvent.click(await screen.findByRole('button', { name: 'Sign out' }))

    expect(await screen.findByRole('form', { name: 'Sign in' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/login')
    expect(getAccessToken()).toBeNull()
  })

  it('loads the current user again after signing out and back in', async () => {
    signedIn()
    const fetchMock = fakeApi()
    vi.stubGlobal('fetch', fetchMock)
    renderAt('/')
    await screen.findByText('System Administrator')
    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }))
    await screen.findByRole('form', { name: 'Sign in' })

    submitSignIn('admin@crm.local', ADMIN_PASSWORD)

    expect(await screen.findByText('System Administrator')).toBeInTheDocument()
    expect(callsTo(fetchMock, '/api/auth/me')).toBe(2)
  })

  it('returns to /login when the stored token is rejected', async () => {
    saveSession('expired-token', inOneHour())
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/')

    expect(await screen.findByRole('form', { name: 'Sign in' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/login')
  })

  it('opens the page the user asked for after login', async () => {
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/customers')
    await screen.findByRole('form', { name: 'Sign in' })

    submitSignIn('admin@crm.local', ADMIN_PASSWORD)

    expect(await screen.findByRole('heading', { level: 1, name: 'Customers' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/customers')
  })

  it('redirects /login to the dashboard when already signed in', async () => {
    signedIn()
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/login')

    expect(await screen.findByRole('heading', { level: 1, name: 'Dashboard' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/')
  })

  it('redirects an unknown path to the dashboard', async () => {
    signedIn()
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/no-such-page')

    expect(await screen.findByRole('heading', { level: 1, name: 'Dashboard' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/')
  })

  it('opens a page for every navigation item (areas not built yet say "coming soon")', async () => {
    signedIn()
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/')
    const navigation = await screen.findByRole('navigation', { name: 'Main navigation' })

    for (const label of NAVIGATION_LABELS.slice(1)) {
      fireEvent.click(within(navigation).getByRole('link', { name: label }))

      expect(await screen.findByRole('heading', { level: 1, name: label })).toBeInTheDocument()
      expect(screen.getByText('This area is coming soon.')).toBeInTheDocument()
      expect(within(navigation).getByRole('link', { name: label })).toHaveAttribute('aria-current', 'page')
    }
  })
})

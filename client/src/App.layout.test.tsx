import { fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { getAccessToken, saveSession } from './auth/session'
import { ADMIN_PASSWORD, callsTo, fakeApi, inOneHour, submitSignIn } from './test/fake-api'

const NAVIGATION_LABELS = ['Dashboard', 'Tickets', 'Customers', 'Tasks', 'Live chat', 'Quick replies', 'Knowledge base', 'Reports', 'Users', 'Assignment', 'Ticket categories', 'Departments', 'Branches', 'SLA policy', 'Audit log', 'Web forms', 'Integrations', 'Branding', 'Settings']
/** Areas whose story is not built yet (each later story removes its label from this list). */
const COMING_SOON_LABELS: string[] = []

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

  it('redirects / to the welcome page when signed out', async () => {
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/')

    expect(await screen.findByRole('heading', { level: 1, name: 'Customer support, all in one place' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/welcome')
  })

  it('shows the welcome page at /welcome and its Sign in link opens the login page', async () => {
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/welcome')

    fireEvent.click(await screen.findByRole('link', { name: 'Sign in' }))

    expect(await screen.findByRole('form', { name: 'Sign in' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/login')
  })

  it('still redirects other protected pages to /login when signed out', async () => {
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/tickets')

    expect(await screen.findByRole('form', { name: 'Sign in' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/login')
  })

  it('lands on the dashboard with the sidebar navigation after a successful login', async () => {
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/login')
    await screen.findByRole('form', { name: 'Sign in' })

    submitSignIn('admin@crm.local', ADMIN_PASSWORD)

    expect(await screen.findByRole('heading', { level: 1, name: 'Dashboard' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/')
    const navigation = screen.getByRole('navigation', { name: 'Main navigation' })
    // Items that need a permission appear once GET /api/auth/me has answered (the SuperAdmin sees all of them).
    await within(navigation).findByRole('link', { name: 'Users' })
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

  it('signs out: clears the token and returns to the welcome page', async () => {
    signedIn()
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/')

    fireEvent.click(await screen.findByRole('button', { name: 'Sign out' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Customer support, all in one place' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/welcome')
    expect(getAccessToken()).toBeNull()
  })

  it('loads the current user again after signing out and back in', async () => {
    signedIn()
    const fetchMock = fakeApi()
    vi.stubGlobal('fetch', fetchMock)
    renderAt('/')
    await screen.findByText('System Administrator')
    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }))
    fireEvent.click(await screen.findByRole('link', { name: 'Sign in' }))
    await screen.findByRole('form', { name: 'Sign in' })

    submitSignIn('admin@crm.local', ADMIN_PASSWORD)

    expect(await screen.findByText('System Administrator')).toBeInTheDocument()
    expect(callsTo(fetchMock, '/api/auth/me')).toBe(2)
  })

  it('returns to /login when the stored token is rejected', async () => {
    saveSession('expired-token', inOneHour())
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/tickets')

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

  it('opens the users page from the sidebar', async () => {
    signedIn()
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/')
    const navigation = await screen.findByRole('navigation', { name: 'Main navigation' })

    fireEvent.click(await within(navigation).findByRole('link', { name: 'Users' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Users' })).toBeInTheDocument()
    expect(await screen.findByRole('row', { name: /System Administrator/ })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/users')
    expect(within(navigation).getByRole('link', { name: 'Users' })).toHaveAttribute('aria-current', 'page')
  })

  it('opens the customers page from the sidebar', async () => {
    signedIn()
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/')
    const navigation = await screen.findByRole('navigation', { name: 'Main navigation' })

    fireEvent.click(await within(navigation).findByRole('link', { name: 'Customers' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Customers' })).toBeInTheDocument()
    expect(await screen.findByRole('row', { name: /Nour Trading/ })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/customers')
    expect(within(navigation).getByRole('link', { name: 'Customers' })).toHaveAttribute('aria-current', 'page')
  })

  it('opens the tickets page from the sidebar', async () => {
    signedIn()
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/')
    const navigation = await screen.findByRole('navigation', { name: 'Main navigation' })

    fireEvent.click(await within(navigation).findByRole('link', { name: 'Tickets' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Tickets' })).toBeInTheDocument()
    expect(await screen.findByRole('button', { name: 'New ticket' })).toBeInTheDocument()
    expect(await screen.findByRole('row', { name: /TKT-000001/ })).toBeInTheDocument()
    expect(screen.queryByText('This area is coming soon.')).not.toBeInTheDocument()
    expect(window.location.pathname).toBe('/tickets')
  })

  it('opens a page for every navigation item (areas not built yet say "coming soon")', async () => {
    signedIn()
    vi.stubGlobal('fetch', fakeApi())
    renderAt('/')
    const navigation = await screen.findByRole('navigation', { name: 'Main navigation' })

    for (const label of COMING_SOON_LABELS) {
      fireEvent.click(await within(navigation).findByRole('link', { name: label }))

      expect(await screen.findByRole('heading', { level: 1, name: label })).toBeInTheDocument()
      expect(screen.getByText('This area is coming soon.')).toBeInTheDocument()
      expect(within(navigation).getByRole('link', { name: label })).toHaveAttribute('aria-current', 'page')
    }
  })
})

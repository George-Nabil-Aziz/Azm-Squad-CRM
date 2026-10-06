import { render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { permissions } from './auth/permissions'
import { saveSession } from './auth/session'
import { agentMe, callsTo, fakeApi, inOneHour, supervisorMe } from './test/fake-api'

function renderSignedInAt(path: string) {
  saveSession('good-token', inOneHour())
  window.history.replaceState(null, '', path)
  return render(<App />)
}

async function navigationLinks() {
  const navigation = await screen.findByRole('navigation', { name: 'Main navigation' })
  // Items that need a permission appear once GET /api/auth/me has answered.
  await within(navigation).findByRole('link', { name: 'Tickets' })
  return within(navigation).getAllByRole('link').map((link) => link.textContent)
}

describe('Menu and pages follow the user permissions', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('hides the menu items an agent has no permission for', async () => {
    vi.stubGlobal('fetch', fakeApi({ me: agentMe }))
    renderSignedInAt('/')

    expect(await navigationLinks()).toEqual(['Dashboard', 'Tickets', 'Customers', 'Knowledge base'])
  })

  it('shows Reports but not Users to a supervisor', async () => {
    vi.stubGlobal('fetch', fakeApi({ me: supervisorMe }))
    renderSignedInAt('/')

    expect(await navigationLinks()).toEqual(['Dashboard', 'Tickets', 'Customers', 'Knowledge base', 'Reports'])
  })

  it('sends a user without permission from /users to the dashboard without calling the users API', async () => {
    const fetchMock = fakeApi({ me: agentMe })
    vi.stubGlobal('fetch', fetchMock)
    renderSignedInAt('/users')

    expect(await screen.findByRole('heading', { level: 1, name: 'Dashboard' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/')
    expect(fetchMock.mock.calls.some(([path]) => String(path).startsWith('/api/users'))).toBe(false)
    expect(callsTo(fetchMock, '/api/auth/me')).toBe(1)
  })

  it('opens /users for a user with the permission', async () => {
    vi.stubGlobal('fetch', fakeApi())
    renderSignedInAt('/users')

    expect(await screen.findByRole('heading', { level: 1, name: 'Users' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/users')
  })
  it('shows the Audit log to an admin but not to a supervisor', async () => {
    const adminMe = { ...agentMe, id: '9', roles: ['Admin' as const], permissions: [...agentMe.permissions, permissions.auditView] }
    vi.stubGlobal('fetch', fakeApi({ me: adminMe }))
    renderSignedInAt('/')

    expect(await navigationLinks()).toContain('Audit log')
  })

  it('sends a supervisor from /audit-logs to the dashboard', async () => {
    vi.stubGlobal('fetch', fakeApi({ me: supervisorMe }))
    renderSignedInAt('/audit-logs')

    expect(await screen.findByRole('heading', { level: 1, name: 'Dashboard' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/')
  })
  it('opens the reports area for a supervisor, on the dashboard', async () => {
    vi.stubGlobal('fetch', fakeApi({ me: supervisorMe }))
    renderSignedInAt('/reports')

    expect(await screen.findByRole('heading', { level: 1, name: 'Reports' })).toBeInTheDocument()
    expect(window.location.pathname).toBe('/reports/dashboard')
    expect(await screen.findByRole('link', { name: 'Dashboard', current: 'page' })).toBeInTheDocument()
  })

  it('sends an agent from /reports to the dashboard without calling the reports API', async () => {
    const fetchMock = fakeApi({ me: agentMe })
    vi.stubGlobal('fetch', fetchMock)
    renderSignedInAt('/reports/tickets')

    expect(await screen.findByRole('heading', { level: 1, name: 'Dashboard' })).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([path]) => String(path).startsWith('/api/reports'))).toBe(false)
  })
})

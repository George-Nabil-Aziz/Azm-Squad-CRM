import { render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'
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

    expect(await navigationLinks()).toEqual(['Dashboard', 'Tickets', 'Customers', 'Knowledge base', 'Reports', 'Assignment'])
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
})

import { fireEvent, screen } from '@testing-library/react'
import { vi } from 'vitest'

export const ADMIN_PASSWORD = 'Admin#12345'

export const inOneHour = () => new Date(Date.now() + 60 * 60 * 1000).toISOString()

function json(status: number, body: unknown, contentType = 'application/json') {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': contentType } })
}

const me = { id: '1', email: 'admin@crm.local', fullName: 'System Administrator', roles: ['SuperAdmin'] }

interface FakeApiOptions {
  /** Status returned by GET /api/health (default 200). */
  healthStatus?: number
}

/**
 * Fake API behind a stubbed fetch: login accepts ADMIN_PASSWORD and returns "good-token";
 * /api/auth/me needs that token; /api/health is ok unless healthStatus says otherwise.
 */
export function fakeApi({ healthStatus = 200 }: FakeApiOptions = {}) {
  return vi.fn(async (path: string, init?: RequestInit) => {
    if (path === '/api/health') {
      return healthStatus === 200
        ? json(200, { status: 'ok' })
        : json(healthStatus, { status: healthStatus, correlationId: 'health-1' }, 'application/problem+json')
    }
    if (path === '/api/auth/login') {
      const { password } = JSON.parse(String(init?.body)) as { password: string }
      return password === ADMIN_PASSWORD
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

/** Number of fetch calls made to `path`. */
export function callsTo(fetchMock: ReturnType<typeof fakeApi>, path: string): number {
  return fetchMock.mock.calls.filter(([calledPath]) => calledPath === path).length
}

export function submitSignIn(email: string, password: string) {
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: email } })
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: password } })
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))
}

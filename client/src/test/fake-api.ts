import { fireEvent, screen } from '@testing-library/react'
import { vi } from 'vitest'
import type { CurrentUser } from '@/api/auth'
import { permissions } from '@/auth/permissions'

export const ADMIN_PASSWORD = 'Admin#12345'

export const inOneHour = () => new Date(Date.now() + 60 * 60 * 1000).toISOString()

function json(status: number, body: unknown, contentType = 'application/json') {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': contentType } })
}

/** GET /api/auth/me of the seeded SuperAdmin: every permission. */
export const superAdminMe: CurrentUser = {
  id: '1',
  email: 'admin@crm.local',
  fullName: 'System Administrator',
  roles: ['SuperAdmin'],
  permissions: Object.values(permissions),
}

/** Same permissions as the server gives the Agent role (Crm.Application.Auth.RolePermissions). */
export const agentMe: CurrentUser = {
  id: '2',
  email: 'agent@crm.local',
  fullName: 'Sara Agent',
  roles: ['Agent'],
  permissions: [
    permissions.customersView,
    permissions.customersManage,
    permissions.ticketsView,
    permissions.ticketsManage,
    permissions.kbView,
    permissions.notificationsView,
    permissions.tasksManage,
  ],
}

/** Same permissions as the server gives the Supervisor role. */
export const supervisorMe: CurrentUser = {
  id: '3',
  email: 'lead@crm.local',
  fullName: 'Team Lead',
  roles: ['Supervisor'],
  permissions: [...agentMe.permissions, permissions.ticketsAssign, permissions.reportsView, permissions.quickRepliesManageShared],
}

interface FakeApiOptions {
  /** Status returned by GET /api/health (default 200). */
  healthStatus?: number
  /** Body of GET /api/auth/me (default: the SuperAdmin). */
  me?: CurrentUser
}

/**
 * Fake API behind a stubbed fetch: login accepts ADMIN_PASSWORD and returns "good-token";
 * /api/auth/me needs that token and returns `me`; /api/health is ok unless healthStatus says otherwise.
 */
export function fakeApi({ healthStatus = 200, me = superAdminMe }: FakeApiOptions = {}) {
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
    if (path === '/api/users' || path.startsWith('/api/users?')) {
      const { id, email, fullName, roles } = superAdminMe
      return json(200, { items: [{ id, email, fullName, roles, isActive: true }], page: 1, pageSize: 20, totalCount: 1 })
    }
    if (path === '/api/customers' || path.startsWith('/api/customers?')) {
      const customer = {
        id: 'c1',
        name: 'Nour Trading',
        email: 'info@nour.example',
        phone: '+966 50 123 4567',
        contacts: [],
        createdAt: '2026-10-01T08:00:00Z',
        updatedAt: '2026-10-01T08:00:00Z',
      }
      return json(200, { items: [customer], page: 1, pageSize: 20, totalCount: 1 })
    }
    if (path === '/api/notifications/unread-count') {
      return json(200, { count: 0 })
    }
    if (path.startsWith('/api/notifications')) {
      return json(200, { items: [], page: 1, pageSize: 20, totalCount: 0 })
    }
    if (path.startsWith('/api/tickets/mine')) {
      return json(200, { counters: { open: 0, pending: 0, breachedToday: 0 }, tickets: { items: [], page: 1, pageSize: 50, totalCount: 0 } })
    }
    if (path === '/api/tickets/assignees') {
      return json(200, [{ id: superAdminMe.id, fullName: superAdminMe.fullName }])
    }
    if (path === '/api/tickets' || path.startsWith('/api/tickets?')) {
      const ticket = {
        id: 't1',
        number: 'TKT-000001',
        subject: 'Invoice is wrong',
        description: null,
        status: 'new',
        priority: 'mid',
        channel: 'manual',
        customerId: 'c1',
        customerName: 'Nour Trading',
        categoryId: null,
        categoryName: null,
        assigneeId: null,
        assigneeName: null,
        createdAt: '2026-10-01T08:00:00Z',
        updatedAt: '2026-10-01T08:00:00Z',
        firstResponseAt: null,
        resolvedAt: null,
        allowedStatuses: [],
      }
      return json(200, { items: [ticket], page: 1, pageSize: 20, totalCount: 1 })
    }
    if (path === '/api/ticket-categories' || path.startsWith('/api/ticket-categories?')) {
      return json(200, [])
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

import { QueryClientProvider } from '@tanstack/react-query'
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getCurrentUser, type CurrentUser } from '@/api/auth'
import { connectNotificationsHub } from '@/api/notifications-hub'
import {
  getUnreadCount,
  listNotifications,
  markAllNotificationsRead,
  markNotificationRead,
  type AppNotification,
  type NotificationPush,
} from '@/api/notifications'
import { createQueryClient } from '@/app/query-client'
import { Toaster } from '@/components/ui/sonner'
import { permissions } from '@/auth/permissions'
import { NotificationBell } from './NotificationBell'

vi.mock('@/api/auth', () => ({ getCurrentUser: vi.fn() }))
vi.mock('@/api/notifications', () => ({
  getUnreadCount: vi.fn(),
  listNotifications: vi.fn(),
  markNotificationRead: vi.fn(),
  markAllNotificationsRead: vi.fn(),
}))
vi.mock('@/api/notifications-hub', () => ({ connectNotificationsHub: vi.fn() }))

const agent: CurrentUser = {
  id: '2',
  email: 'agent@crm.local',
  fullName: 'Sara Agent',
  roles: ['Agent'],
  permissions: [permissions.ticketsView, permissions.notificationsView],
}

const assigned: AppNotification = {
  id: 'n1',
  type: 'assignment',
  ticketId: 't1',
  ticketNumber: 'TKT-000001',
  level: 0,
  text: null,
  createdAt: '2026-10-06T08:00:00Z',
  readAt: null,
}
const escalated: AppNotification = { ...assigned, id: 'n2', type: 'slaEscalation', level: 2, ticketNumber: 'TKT-000002', ticketId: 't2' }
const old: AppNotification = { ...assigned, id: 'n3', type: 'slaWarning', ticketNumber: 'TKT-000003', readAt: '2026-10-06T09:00:00Z' }

function renderBell() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter>
        <NotificationBell />
        <Toaster />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('NotificationBell', () => {
  let push: (value: NotificationPush) => void

  beforeEach(() => {
    vi.mocked(getCurrentUser).mockReset().mockResolvedValue(agent)
    vi.mocked(getUnreadCount).mockReset().mockResolvedValue({ count: 2 })
    vi.mocked(listNotifications)
      .mockReset()
      .mockResolvedValue({ items: [assigned, escalated, old], page: 1, pageSize: 20, totalCount: 3 })
    vi.mocked(markNotificationRead).mockReset().mockResolvedValue({ ...assigned, readAt: '2026-10-06T10:00:00Z' })
    vi.mocked(markAllNotificationsRead).mockReset().mockResolvedValue({ count: 0 })
    vi.mocked(connectNotificationsHub)
      .mockReset()
      .mockImplementation((onNotification) => {
        push = onNotification
        return () => {}
      })
  })

  it('shows the unread count on the bell', async () => {
    renderBell()

    expect(await screen.findByRole('button', { name: 'Notifications (2 unread)' })).toBeInTheDocument()
  })

  it('is hidden from a user without notifications.view', async () => {
    vi.mocked(getCurrentUser).mockResolvedValue({ ...agent, permissions: [permissions.ticketsView] })
    renderBell()
    await waitFor(() => expect(getCurrentUser).toHaveBeenCalled())

    expect(screen.queryByRole('button', { name: /Notifications/ })).not.toBeInTheDocument()
  })

  it('lists the notifications with a message and a link to the ticket', async () => {
    renderBell()

    fireEvent.click(await screen.findByRole('button', { name: 'Notifications (2 unread)' }))

    const panel = await screen.findByRole('dialog', { name: 'Notifications' })
    expect(await within(panel).findByText('Ticket TKT-000001 was assigned to you.')).toBeInTheDocument()
    expect(within(panel).getByText('Ticket TKT-000002 breached its SLA (escalation level 2).')).toBeInTheDocument()
    expect(within(panel).getByText('Ticket TKT-000003 is close to its response deadline.')).toBeInTheDocument()
    expect(within(panel).getByRole('link', { name: /TKT-000001 was assigned/ })).toHaveAttribute('href', '/tickets/t1')
  })

  it('marks one notification read and shows the new count', async () => {
    renderBell()
    fireEvent.click(await screen.findByRole('button', { name: 'Notifications (2 unread)' }))
    const panel = await screen.findByRole('dialog', { name: 'Notifications' })
    await within(panel).findByText('Ticket TKT-000001 was assigned to you.')
    vi.mocked(getUnreadCount).mockResolvedValue({ count: 1 })

    fireEvent.click(within(panel).getAllByRole('button', { name: 'Mark as read' })[0])

    await waitFor(() => expect(markNotificationRead).toHaveBeenCalledWith('n1'))
    fireEvent.keyDown(panel, { key: 'Escape' }) // the bell behind the modal panel is only reachable once it is closed
    expect(await screen.findByRole('button', { name: 'Notifications (1 unread)' })).toBeInTheDocument()
  })

  it('marks all notifications read', async () => {
    renderBell()
    fireEvent.click(await screen.findByRole('button', { name: 'Notifications (2 unread)' }))
    const panel = await screen.findByRole('dialog', { name: 'Notifications' })
    vi.mocked(getUnreadCount).mockResolvedValue({ count: 0 })

    fireEvent.click(await within(panel).findByRole('button', { name: 'Mark all as read' }))

    await waitFor(() => expect(markAllNotificationsRead).toHaveBeenCalled())
    fireEvent.keyDown(panel, { key: 'Escape' })
    expect(await screen.findByRole('button', { name: 'Notifications' })).toBeInTheDocument()
  })

  it('shows a live notification pushed by the hub and its unread count', async () => {
    renderBell()
    await screen.findByRole('button', { name: 'Notifications (2 unread)' })
    await waitFor(() => expect(connectNotificationsHub).toHaveBeenCalled())

    act(() => push({ notification: { ...assigned, id: 'n9' }, unreadCount: 3 }))

    expect(await screen.findByRole('button', { name: 'Notifications (3 unread)' })).toBeInTheDocument()
    expect(await screen.findByText('Ticket TKT-000001 was assigned to you.')).toBeInTheDocument() // the toast
  })

  it('shows an empty message when there are no notifications', async () => {
    vi.mocked(getUnreadCount).mockResolvedValue({ count: 0 })
    vi.mocked(listNotifications).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    renderBell()

    fireEvent.click(await screen.findByRole('button', { name: 'Notifications' }))

    expect(await screen.findByText('You have no notifications.')).toBeInTheDocument()
  })
})

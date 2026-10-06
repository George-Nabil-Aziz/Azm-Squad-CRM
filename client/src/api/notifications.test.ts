import { afterEach, describe, expect, it, vi } from 'vitest'
import { getUnreadCount, listNotifications, markAllNotificationsRead, markNotificationRead } from './notifications'

function fakeFetch(body: unknown = {}) {
  const fetchMock = vi
    .fn()
    .mockResolvedValue(new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } }))
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function sent(fetchMock: ReturnType<typeof vi.fn>) {
  const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit]
  return { path, method: init.method }
}

describe('notifications API', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lists the notifications with GET /api/notifications', async () => {
    const fetchMock = fakeFetch({ items: [], page: 1, pageSize: 20, totalCount: 0 })

    await listNotifications()

    expect(sent(fetchMock)).toEqual({ path: '/api/notifications?pageSize=20', method: 'GET' })
  })

  it('can ask for unread notifications only', async () => {
    const fetchMock = fakeFetch({ items: [], page: 1, pageSize: 20, totalCount: 0 })

    await listNotifications({ unreadOnly: true, pageSize: 5 })

    expect(sent(fetchMock).path).toBe('/api/notifications?pageSize=5&unreadOnly=true')
  })

  it('reads the unread count', async () => {
    const fetchMock = fakeFetch({ count: 3 })

    expect(await getUnreadCount()).toEqual({ count: 3 })
    expect(sent(fetchMock).path).toBe('/api/notifications/unread-count')
  })

  it('marks one notification read with POST', async () => {
    const fetchMock = fakeFetch({ id: 'n1' })

    await markNotificationRead('n1')

    expect(sent(fetchMock)).toEqual({ path: '/api/notifications/n1/read', method: 'POST' })
  })

  it('marks all notifications read with POST', async () => {
    const fetchMock = fakeFetch({ count: 0 })

    await markAllNotificationsRead()

    expect(sent(fetchMock)).toEqual({ path: '/api/notifications/read-all', method: 'POST' })
  })
})

import { afterEach, describe, expect, it, vi } from 'vitest'
import { clearSession, getAccessToken, saveSession, subscribeToSession } from './session'

const inOneHour = () => new Date(Date.now() + 60 * 60 * 1000).toISOString()

describe('session', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('has no token when nothing is stored', () => {
    expect(getAccessToken()).toBeNull()
  })

  it('returns the saved token until it expires', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-01-01T10:00:00Z'))
    saveSession('token-1', '2026-01-01T11:00:00Z')

    expect(getAccessToken()).toBe('token-1')

    vi.setSystemTime(new Date('2026-01-01T11:00:01Z'))
    expect(getAccessToken()).toBeNull()
  })

  it('notifies subscribers on save and clear, and forgets the token on clear', () => {
    const listener = vi.fn()
    const unsubscribe = subscribeToSession(listener)

    saveSession('token-2', inOneHour())
    clearSession()
    unsubscribe()
    saveSession('token-3', inOneHour())

    expect(listener).toHaveBeenCalledTimes(2)
    expect(getAccessToken()).toBe('token-3')
    clearSession()
    expect(getAccessToken()).toBeNull()
  })

  it('ignores corrupt stored data', () => {
    localStorage.setItem('crm.session', '{not json')

    expect(getAccessToken()).toBeNull()
  })
})

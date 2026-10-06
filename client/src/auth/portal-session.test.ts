import { afterEach, describe, expect, it } from 'vitest'
import { clearPortalSession, getPortalAccessToken, getPortalCustomer, savePortalSession } from './portal-session'
import { getAccessToken } from './session'

const customer = { id: 'c1', name: 'Nour', email: 'n@x.example' }

describe('portal session', () => {
  afterEach(() => {
    clearPortalSession()
    localStorage.clear()
  })

  it('stores the token and customer apart from the staff session', () => {
    savePortalSession('t', new Date(Date.now() + 60_000).toISOString(), customer)

    expect(getPortalAccessToken()).toBe('t')
    expect(getPortalCustomer()).toEqual(customer)
    expect(getAccessToken()).toBeNull()
  })

  it('ignores an expired token', () => {
    savePortalSession('t', new Date(Date.now() - 1000).toISOString(), customer)

    expect(getPortalAccessToken()).toBeNull()
  })

  it('clears on sign-out', () => {
    savePortalSession('t', new Date(Date.now() + 60_000).toISOString(), customer)
    clearPortalSession()

    expect(getPortalCustomer()).toBeNull()
  })
})

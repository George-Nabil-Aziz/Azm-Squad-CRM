import { useSyncExternalStore } from 'react'
import { getPortalAccessToken, getPortalCustomer, subscribeToPortalSession, type PortalCustomer } from './portal-session'

/** True while a non-expired portal token is stored. Re-renders on portal sign-in / sign-out. */
export function useIsPortalAuthenticated(): boolean {
  return useSyncExternalStore(subscribeToPortalSession, () => getPortalAccessToken() !== null)
}

let cachedKey = ''
let cachedCustomer: PortalCustomer | null = null

/** A stable snapshot of the signed-in customer (useSyncExternalStore needs the same object while nothing changed). */
function getCustomerSnapshot(): PortalCustomer | null {
  const customer = getPortalCustomer()
  const key = customer ? `${customer.id}|${customer.name}|${customer.email}` : ''
  if (key !== cachedKey) {
    cachedKey = key
    cachedCustomer = customer
  }
  return cachedCustomer
}

/** The signed-in portal customer, or null. */
export function usePortalCustomer(): PortalCustomer | null {
  return useSyncExternalStore(subscribeToPortalSession, getCustomerSnapshot)
}

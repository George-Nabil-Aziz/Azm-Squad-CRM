import { useSyncExternalStore } from 'react'
import { getAccessToken, subscribeToSession } from './session'

function getIsAuthenticated(): boolean {
  return getAccessToken() !== null
}

/** True while a non-expired access token is stored. Re-renders on sign-in / sign-out. */
export function useIsAuthenticated(): boolean {
  return useSyncExternalStore(subscribeToSession, getIsAuthenticated)
}

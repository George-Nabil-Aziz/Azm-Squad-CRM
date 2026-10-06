import type { LoginRedirectState } from './return-path'

/** Where the portal sign-in page is. */
export const PORTAL_LOGIN_PATH = '/portal/login'

/**
 * Where to go after a portal sign-in: the stored in-app portal path, or "/portal" when it is missing or unsafe
 * (only paths below /portal are allowed; never the sign-in page itself).
 */
export function getPortalReturnPath(state: unknown): string {
  const from = (state as Partial<LoginRedirectState> | null | undefined)?.from
  if (typeof from !== 'string') return '/portal'
  if (from !== '/portal' && !from.startsWith('/portal/') && !from.startsWith('/portal?')) return '/portal'
  if (from === PORTAL_LOGIN_PATH || from.startsWith(`${PORTAL_LOGIN_PATH}?`)) return '/portal'
  return from
}

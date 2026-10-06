import { Navigate, Outlet, useLocation } from 'react-router'
import { useIsPortalAuthenticated } from '@/auth/usePortalSession'
import { PORTAL_LOGIN_PATH } from './portal-paths'
import type { LoginRedirectState } from './return-path'

/** Renders the child routes only for a signed-in portal customer; otherwise redirects to the portal sign-in page. */
export function RequirePortalAuth() {
  const isAuthenticated = useIsPortalAuthenticated()
  const location = useLocation()

  if (!isAuthenticated) {
    const state: LoginRedirectState = { from: location.pathname + location.search }
    return <Navigate to={PORTAL_LOGIN_PATH} replace state={state} />
  }

  return <Outlet />
}

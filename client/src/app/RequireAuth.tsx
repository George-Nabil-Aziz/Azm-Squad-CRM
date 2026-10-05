import { Navigate, Outlet, useLocation } from 'react-router'
import { useIsAuthenticated } from '@/auth/useIsAuthenticated'
import type { LoginRedirectState } from './return-path'

/** Renders the child routes only when signed in; otherwise redirects to /login and remembers the page. */
export function RequireAuth() {
  const isAuthenticated = useIsAuthenticated()
  const location = useLocation()

  if (!isAuthenticated) {
    const state: LoginRedirectState = { from: location.pathname + location.search }
    return <Navigate to="/login" replace state={state} />
  }

  return <Outlet />
}

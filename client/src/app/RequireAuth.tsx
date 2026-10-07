import { Navigate, Outlet, useLocation } from 'react-router'
import { useIsAuthenticated } from '@/auth/useIsAuthenticated'
import { LandingPage } from '@/pages/landing/LandingPage'
import type { LoginRedirectState } from './return-path'

/** Renders the child routes only when signed in; otherwise redirects to /login and remembers the page. */
export function RequireAuth() {
  const isAuthenticated = useIsAuthenticated()
  const location = useLocation()

  // A visitor who is not signed in sees the landing page at / itself; every other protected page goes to /login.
  if (!isAuthenticated && location.pathname === '/') return <LandingPage />

  if (!isAuthenticated) {
    const state: LoginRedirectState = { from: location.pathname + location.search }
    return <Navigate to="/login" replace state={state} />
  }

  return <Outlet />
}

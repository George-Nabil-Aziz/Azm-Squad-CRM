import { Navigate, Outlet } from 'react-router'
import type { Permission } from '@/auth/permissions'
import { usePermissions } from '@/features/auth/usePermissions'

/**
 * Renders the child routes only for users with the permission; others go to the dashboard (no page loads,
 * no API call). Renders nothing while the permissions load. Place it inside RequireAuth.
 */
export function RequirePermission({ permission }: { permission: Permission }) {
  const { can, isLoading } = usePermissions()

  if (isLoading) return null
  return can(permission) ? <Outlet /> : <Navigate to="/" replace />
}

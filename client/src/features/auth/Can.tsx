import type { ReactNode } from 'react'
import type { Permission } from '@/auth/permissions'
import { usePermissions } from './usePermissions'

/** Renders its children only for users with the permission (hides UI only; the API still checks it). */
export function Can({ permission, children }: { permission: Permission; children: ReactNode }) {
  const { can } = usePermissions()

  return can(permission) ? children : null
}

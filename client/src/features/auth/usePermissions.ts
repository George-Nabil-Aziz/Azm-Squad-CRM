import type { Permission } from '@/auth/permissions'
import { useCurrentUser } from './useCurrentUser'

/**
 * The signed-in user's permissions (from GET /api/auth/me, shared React Query cache).
 * `can` answers false while they are loading, so nothing protected flashes up before the answer.
 */
export function usePermissions() {
  const { data: user, isPending } = useCurrentUser()

  return {
    isLoading: isPending,
    can: (permission: Permission) => user?.permissions.includes(permission) ?? false,
  }
}

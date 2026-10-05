import { useQuery } from '@tanstack/react-query'
import { getCurrentUser } from '@/api/auth'

export const currentUserQueryKey = ['auth', 'me'] as const

/** The signed-in user (GET /api/auth/me). A 401 clears the session, so RequireAuth redirects to /login. */
export function useCurrentUser() {
  return useQuery({
    queryKey: currentUserQueryKey,
    queryFn: ({ signal }) => getCurrentUser(signal),
  })
}

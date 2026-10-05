import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { listUsers, type ListUsersParams } from '@/api/users'

/** Prefix of every users query: mutations invalidate it so every page and search reloads. */
export const usersQueryKey = ['users'] as const

/** One page of GET /api/users. The previous page stays visible while the next one loads. */
export function useUsers(params: ListUsersParams) {
  return useQuery({
    queryKey: [...usersQueryKey, params],
    queryFn: ({ signal }) => listUsers(params, signal),
    placeholderData: keepPreviousData,
  })
}

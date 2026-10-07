import { useQuery } from '@tanstack/react-query'
import { listBranches, type BranchListParams } from '@/api/branches'

/** Prefix of every branches query: mutations invalidate it so every list reloads. */
export const branchesQueryKey = ['branches'] as const

/** GET /api/branches (all branches, or only the active ones). A branch user gets only their own. */
export function useBranches(params: BranchListParams, enabled = true) {
  return useQuery({
    queryKey: [...branchesQueryKey, params],
    queryFn: ({ signal }) => listBranches(params, signal),
    enabled,
  })
}

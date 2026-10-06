import { useQuery } from '@tanstack/react-query'
import { listSlaPolicies } from '@/api/sla-policies'

/** Key of the SLA policies query: the edit dialog invalidates it so the table reloads. */
export const slaPoliciesQueryKey = ['sla-policies'] as const

/** GET /api/sla-policies (High, Mid, Low). */
export function useSlaPolicies() {
  return useQuery({
    queryKey: slaPoliciesQueryKey,
    queryFn: ({ signal }) => listSlaPolicies(signal),
  })
}

import { useQuery } from '@tanstack/react-query'
import { getAiStatus } from '@/api/ai'

/** Prefix of every AI query. */
export const aiQueryKey = ['ai'] as const

/** Whether the server has an AI provider key. Nothing AI-related is shown until it says `enabled`. */
export function useAiStatus() {
  return useQuery({
    queryKey: [...aiQueryKey, 'status'],
    queryFn: ({ signal }) => getAiStatus(signal),
    staleTime: 5 * 60 * 1000,
  })
}

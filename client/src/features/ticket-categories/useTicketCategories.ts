import { useQuery } from '@tanstack/react-query'
import { listTicketCategories, type TicketCategoryListParams } from '@/api/ticket-categories'

/** Prefix of every ticket-categories query: mutations invalidate it so every list reloads. */
export const ticketCategoriesQueryKey = ['ticket-categories'] as const

/** GET /api/ticket-categories (all categories, or only the active ones). */
export function useTicketCategories(params: TicketCategoryListParams) {
  return useQuery({
    queryKey: [...ticketCategoriesQueryKey, params],
    queryFn: ({ signal }) => listTicketCategories(params, signal),
  })
}

import { useQuery } from '@tanstack/react-query'
import { getTicketHistory } from '@/api/tickets'
import { ticketsQueryKey } from './useTickets'

/** The audit trail of a ticket, oldest first (reloads with every tickets invalidation). */
export function useTicketHistory(id: string) {
  return useQuery({
    queryKey: [...ticketsQueryKey, 'history', id],
    queryFn: ({ signal }) => getTicketHistory(id, signal),
  })
}

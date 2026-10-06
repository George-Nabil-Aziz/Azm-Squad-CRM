import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { getTicket, listTicketAssignees, listTicketMessages, listTickets, type TicketListParams } from '@/api/tickets'

/** Prefix of every tickets query: creating or changing a ticket invalidates it so every list and page reloads. */
export const ticketsQueryKey = ['tickets'] as const

/** One page of GET /api/tickets. The previous page stays visible while the next one loads. */
export function useTickets(params: TicketListParams) {
  return useQuery({
    queryKey: [...ticketsQueryKey, params],
    queryFn: ({ signal }) => listTickets(params, signal),
    placeholderData: keepPreviousData,
  })
}

/** One ticket (GET /api/tickets/{id}). */
export function useTicket(id: string) {
  return useQuery({
    queryKey: [...ticketsQueryKey, 'detail', id],
    queryFn: ({ signal }) => getTicket(id, signal),
  })
}

/** The conversation of a ticket, oldest first. */
export function useTicketMessages(id: string) {
  return useQuery({
    queryKey: [...ticketsQueryKey, 'messages', id],
    queryFn: ({ signal }) => listTicketMessages(id, signal),
  })
}

/** Staff users for the assignee filter (and later the assign picker). */
export function useTicketAssignees() {
  return useQuery({
    queryKey: [...ticketsQueryKey, 'assignees'],
    queryFn: ({ signal }) => listTicketAssignees(signal),
  })
}

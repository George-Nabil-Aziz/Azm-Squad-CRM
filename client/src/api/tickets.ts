import type { TicketChannel, TicketPriority, TicketStatus } from '@/features/tickets/ticket-values'
import { apiGet, apiPost } from './client'

/**
 * A ticket (server: TicketResponse). `number` is "TKT-000001"; names of a deleted customer or a deactivated category
 * are still shown. Times are UTC ISO strings.
 */
export interface Ticket {
  id: string
  number: string
  subject: string
  description: string | null
  status: TicketStatus
  priority: TicketPriority
  channel: TicketChannel
  customerId: string
  customerName: string
  categoryId: string | null
  categoryName: string | null
  assigneeId: string | null
  assigneeName: string | null
  createdAt: string
  updatedAt: string
}

/** Body of "create ticket". Customer and subject are required; send null for no description / category. */
export interface CreateTicketRequest {
  customerId: string
  subject: string
  description: string | null
  categoryId: string | null
  priority: TicketPriority
}

/** Creates a ticket in status "new"; the server gives it the next ticket number. */
export function createTicket(request: CreateTicketRequest): Promise<Ticket> {
  return apiPost<Ticket>('/api/tickets', request)
}

export function getTicket(id: string, signal?: AbortSignal): Promise<Ticket> {
  return apiGet<Ticket>(`/api/tickets/${encodeURIComponent(id)}`, signal)
}

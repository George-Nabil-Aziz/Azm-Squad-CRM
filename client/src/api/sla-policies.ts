import type { TicketPriority } from '@/features/tickets/ticket-values'
import { apiGet, apiPut } from './client'

/** SLA targets of one priority, in whole minutes (server: SlaPolicyResponse). */
export interface SlaPolicy {
  priority: TicketPriority
  responseMinutes: number
  resolutionMinutes: number
  updatedAt: string
}

/** Body of PUT /api/sla-policies/{priority}: both > 0 and resolution >= response (the server answers 400 otherwise). */
export interface SlaPolicyRequest {
  responseMinutes: number
  resolutionMinutes: number
}

/** GET /api/sla-policies: High, Mid, Low (needs sla.manage). */
export function listSlaPolicies(signal?: AbortSignal): Promise<SlaPolicy[]> {
  return apiGet<SlaPolicy[]>('/api/sla-policies', signal)
}

export function updateSlaPolicy(priority: TicketPriority, request: SlaPolicyRequest): Promise<SlaPolicy> {
  return apiPut<SlaPolicy>(`/api/sla-policies/${encodeURIComponent(priority)}`, request)
}

import { apiGet, apiPost } from './client'

/** GET /api/ai/status: false while no AI provider key is configured on the server (the UI then hides the AI actions). */
export interface AiStatus {
  enabled: boolean
}

export function getAiStatus(signal?: AbortSignal): Promise<AiStatus> {
  return apiGet<AiStatus>('/api/ai/status', signal)
}

/** The saved AI summary of a ticket (server: TicketSummaryResponse); every field is null while there is none. */
export interface TicketSummary {
  text: string | null
  language: 'ar' | 'en' | null
  generatedAt: string | null
}

export function getTicketSummary(ticketId: string, signal?: AbortSignal): Promise<TicketSummary> {
  return apiGet<TicketSummary>(`/api/tickets/${encodeURIComponent(ticketId)}/ai-summary`, signal)
}

/** Asks the AI for a new summary; it replaces the saved one. A failing AI call is a 502 and changes nothing. */
export function generateTicketSummary(ticketId: string): Promise<TicketSummary> {
  return apiPost<TicketSummary>(`/api/tickets/${encodeURIComponent(ticketId)}/ai-summary`, {})
}

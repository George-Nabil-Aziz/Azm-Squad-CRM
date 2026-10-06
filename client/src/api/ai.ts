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

/** An article a reply draft was based on. */
export interface ReplyDraftSource {
  id: string
  title: string
}

/** A suggested reply (server: ReplyDraftResponse). It is only a draft: the agent reviews it and sends it. */
export interface ReplyDraft {
  draft: string
  language: 'ar' | 'en'
  articles: ReplyDraftSource[]
}

export function generateReplyDraft(ticketId: string): Promise<ReplyDraft> {
  return apiPost<ReplyDraft>(`/api/tickets/${encodeURIComponent(ticketId)}/ai-reply-draft`, {})
}

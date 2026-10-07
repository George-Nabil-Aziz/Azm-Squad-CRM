import { apiGet, apiPost, apiPut } from './client'

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

/** The AI category / priority suggestion of a ticket (server: TicketAiClassificationResponse); `status` "none" has no data. */
export interface TicketClassification {
  status: 'none' | 'applied' | 'suggested'
  suggestedCategoryId: string | null
  suggestedCategoryName: string | null
  suggestedPriority: 'high' | 'mid' | 'low' | null
  /** 0..1 */
  confidence: number | null
  categoryApplied: boolean
  priorityApplied: boolean
  createdAt: string | null
  categoryOverriddenAt: string | null
  priorityOverriddenAt: string | null
}

export function getTicketClassification(ticketId: string, signal?: AbortSignal): Promise<TicketClassification> {
  return apiGet<TicketClassification>(`/api/tickets/${encodeURIComponent(ticketId)}/ai-classification`, signal)
}

/** A suggested knowledge base article for a ticket (server: SuggestedSolutionResponse); `useful` is the user's vote. */
export interface SuggestedSolution {
  articleId: string
  title: string
  summary: string
  useful: boolean | null
}

export function getSuggestions(ticketId: string, signal?: AbortSignal): Promise<SuggestedSolution[]> {
  return apiGet<SuggestedSolution[]>(`/api/tickets/${encodeURIComponent(ticketId)}/ai-suggestions`, signal)
}

export function sendSuggestionFeedback(
  ticketId: string,
  articleId: string,
  useful: boolean,
): Promise<{ articleId: string; useful: boolean }> {
  return apiPut<{ articleId: string; useful: boolean }>(
    `/api/tickets/${encodeURIComponent(ticketId)}/ai-suggestions/${encodeURIComponent(articleId)}/feedback`,
    { useful },
  )
}

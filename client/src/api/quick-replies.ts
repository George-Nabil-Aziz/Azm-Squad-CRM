import { apiDelete, apiGet, apiPost, apiPut } from './client'

/** A quick reply (server: QuickReplyResponse). `isMine`: the signed-in user owns it. */
export interface QuickReply {
  id: string
  title: string
  shortcut: string | null
  body: string
  isShared: boolean
  ownerId: string
  ownerName: string | null
  isMine: boolean
  createdAt: string
  updatedAt: string
}

/** Body of create and edit. `isShared` needs quick-replies.manage-shared (the server answers 403 otherwise). */
export interface QuickReplyRequest {
  title: string
  shortcut: string | null
  body: string
  isShared: boolean
}

/** GET /api/quick-replies: my personal replies plus the shared ones; `search` matches the title or the shortcut. */
export function listQuickReplies(search = '', signal?: AbortSignal): Promise<QuickReply[]> {
  const query = search.trim() ? `?search=${encodeURIComponent(search.trim())}` : ''
  return apiGet<QuickReply[]>(`/api/quick-replies${query}`, signal)
}

export function createQuickReply(request: QuickReplyRequest): Promise<QuickReply> {
  return apiPost<QuickReply>('/api/quick-replies', request)
}

export function updateQuickReply(id: string, request: QuickReplyRequest): Promise<QuickReply> {
  return apiPut<QuickReply>(`/api/quick-replies/${encodeURIComponent(id)}`, request)
}

export function deleteQuickReply(id: string): Promise<void> {
  return apiDelete(`/api/quick-replies/${encodeURIComponent(id)}`)
}

/** POST /api/quick-replies/{id}/render: the reply text with the placeholders replaced by the ticket's data. */
export function renderQuickReply(id: string, ticketId: string): Promise<{ text: string }> {
  return apiPost<{ text: string }>(`/api/quick-replies/${encodeURIComponent(id)}/render`, { ticketId })
}

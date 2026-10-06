import { apiGet, apiPost } from './client'

/** A FAQ in the UI language (server: PortalFaqResponse). */
export interface PortalFaq {
  id: string
  question: string
  answer: string
}

/** A category that has published articles (server: PortalKbCategoryResponse). */
export interface PortalKbCategory {
  id: string
  name: string
  articleCount: number
}

/** A published article in a list (server: PortalKbArticleSummary). */
export interface PortalKbArticleSummary {
  id: string
  title: string
  summary: string
  categoryName: string
}

export interface PortalKbArticle {
  id: string
  title: string
  body: string
  categoryName: string
  helpfulCount: number
  notHelpfulCount: number
  publishedAt: string | null
}

export interface PortalKbSearchResult {
  type: 'article' | 'faq'
  id: string
  title: string
  snippet: string
  score: number
}

/** Everything here is public (no sign-in); the server answers in the language sent with the request (Accept-Language). */
export function listPortalFaqs(signal?: AbortSignal): Promise<PortalFaq[]> {
  return apiGet<PortalFaq[]>('/api/portal/kb/faqs', signal)
}

export function listPortalKbCategories(signal?: AbortSignal): Promise<PortalKbCategory[]> {
  return apiGet<PortalKbCategory[]>('/api/portal/kb/categories', signal)
}

export function listPortalKbArticles(categoryId: string, signal?: AbortSignal): Promise<{ items: PortalKbArticleSummary[]; totalCount: number }> {
  const query = categoryId ? `?categoryId=${encodeURIComponent(categoryId)}&pageSize=100` : '?pageSize=100'
  return apiGet(`/api/portal/kb/articles${query}`, signal)
}

/** 404 for a draft or unknown article. */
export function getPortalKbArticle(id: string, signal?: AbortSignal): Promise<PortalKbArticle> {
  return apiGet<PortalKbArticle>(`/api/portal/kb/articles/${encodeURIComponent(id)}`, signal)
}

export function searchPortalKb(query: string, signal?: AbortSignal): Promise<PortalKbSearchResult[]> {
  return apiGet<PortalKbSearchResult[]>(`/api/portal/kb/search?q=${encodeURIComponent(query)}`, signal)
}

/** The "helpful" vote: returns the counters after it. */
export function sendPortalKbFeedback(id: string, helpful: boolean): Promise<{ helpfulCount: number; notHelpfulCount: number }> {
  return apiPost(`/api/portal/kb/articles/${encodeURIComponent(id)}/feedback`, { helpful })
}

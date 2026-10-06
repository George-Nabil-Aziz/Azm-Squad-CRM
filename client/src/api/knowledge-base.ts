import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { PagedResult } from './paging'

/** A knowledge base category (server: KbCategoryResponse). `name` is the version for the UI language. */
export interface KbCategory {
  id: string
  nameEn: string | null
  nameAr: string | null
  name: string
  articleCount: number
}

/** Body of create / edit category: a name in English and/or Arabic (at least one). */
export interface KbCategoryRequest {
  nameEn: string | null
  nameAr: string | null
}

export type KbArticleStatus = 'draft' | 'published'

/** An article for staff (server: KbArticleResponse): both language versions plus `title` in the UI language. */
export interface KbArticle {
  id: string
  categoryId: string
  categoryName: string
  titleEn: string | null
  bodyEn: string | null
  titleAr: string | null
  bodyAr: string | null
  title: string
  status: KbArticleStatus
  publishedAt: string | null
  helpfulCount: number
  notHelpfulCount: number
  linkedCount: number
  createdAt: string
  updatedAt: string
}

/** Body of create / edit article: a language version is a title + a body (both or neither); at least one version. */
export interface KbArticleRequest {
  categoryId: string
  titleEn: string | null
  bodyEn: string | null
  titleAr: string | null
  bodyAr: string | null
}

export interface KbArticleListParams {
  categoryId?: string
  status?: KbArticleStatus
  search?: string
  page?: number
  pageSize?: number
}

export function listKbCategories(signal?: AbortSignal): Promise<KbCategory[]> {
  return apiGet<KbCategory[]>('/api/kb/categories', signal)
}

export function createKbCategory(request: KbCategoryRequest): Promise<KbCategory> {
  return apiPost<KbCategory>('/api/kb/categories', request)
}

export function updateKbCategory(id: string, request: KbCategoryRequest): Promise<KbCategory> {
  return apiPut<KbCategory>(`/api/kb/categories/${encodeURIComponent(id)}`, request)
}

/** 409 while the category still has articles. */
export function deleteKbCategory(id: string): Promise<void> {
  return apiDelete(`/api/kb/categories/${encodeURIComponent(id)}`)
}

/** One page of articles, newest first. Users without kb.manage only get published articles. */
export function listKbArticles(params: KbArticleListParams, signal?: AbortSignal): Promise<PagedResult<KbArticle>> {
  const query = new URLSearchParams()
  if (params.categoryId) query.set('categoryId', params.categoryId)
  if (params.status) query.set('status', params.status)
  if (params.search) query.set('search', params.search)
  if (params.page !== undefined) query.set('page', String(params.page))
  if (params.pageSize !== undefined) query.set('pageSize', String(params.pageSize))
  const queryString = query.toString()
  return apiGet<PagedResult<KbArticle>>(queryString ? `/api/kb/articles?${queryString}` : '/api/kb/articles', signal)
}

/** Creates the article as a draft (400 on `title` when no language has a title). */
export function createKbArticle(request: KbArticleRequest): Promise<KbArticle> {
  return apiPost<KbArticle>('/api/kb/articles', request)
}

export function updateKbArticle(id: string, request: KbArticleRequest): Promise<KbArticle> {
  return apiPut<KbArticle>(`/api/kb/articles/${encodeURIComponent(id)}`, request)
}

export function publishKbArticle(id: string): Promise<KbArticle> {
  return apiPost<KbArticle>(`/api/kb/articles/${encodeURIComponent(id)}/publish`, {})
}

export function unpublishKbArticle(id: string): Promise<KbArticle> {
  return apiPost<KbArticle>(`/api/kb/articles/${encodeURIComponent(id)}/unpublish`, {})
}

export function deleteKbArticle(id: string): Promise<void> {
  return apiDelete(`/api/kb/articles/${encodeURIComponent(id)}`)
}

/** A FAQ for staff (server: KbFaqResponse): both language versions plus `question` / `answer` in the UI language. */
export interface KbFaq {
  id: string
  questionEn: string | null
  answerEn: string | null
  questionAr: string | null
  answerAr: string | null
  question: string
  answer: string
  /** Lowest first. */
  displayOrder: number
  isPublished: boolean
  createdAt: string
  updatedAt: string
}

/**
 * Body of create / edit FAQ. A language version is a question + an answer (both or neither); at least one version.
 * `displayOrder` null = last on create, unchanged on edit.
 */
export interface KbFaqRequest {
  questionEn: string | null
  answerEn: string | null
  questionAr: string | null
  answerAr: string | null
  displayOrder: number | null
  isPublished: boolean
}

/** Every FAQ ordered by display order (users without kb.manage only get the published ones). */
export function listKbFaqs(signal?: AbortSignal): Promise<KbFaq[]> {
  return apiGet<KbFaq[]>('/api/kb/faqs', signal)
}

export function createKbFaq(request: KbFaqRequest): Promise<KbFaq> {
  return apiPost<KbFaq>('/api/kb/faqs', request)
}

export function updateKbFaq(id: string, request: KbFaqRequest): Promise<KbFaq> {
  return apiPut<KbFaq>(`/api/kb/faqs/${encodeURIComponent(id)}`, request)
}

export function deleteKbFaq(id: string): Promise<void> {
  return apiDelete(`/api/kb/faqs/${encodeURIComponent(id)}`)
}

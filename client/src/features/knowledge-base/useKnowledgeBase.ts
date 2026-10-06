import { useQuery } from '@tanstack/react-query'
import {
  listKbArticles,
  listKbCategories,
  listKbFaqs,
  type KbArticleListParams,
} from '@/api/knowledge-base'

/** Prefix of every knowledge base query: mutations invalidate it so every list reloads. */
export const kbQueryKey = ['knowledge-base'] as const

export function useKbCategories() {
  return useQuery({
    queryKey: [...kbQueryKey, 'categories'],
    queryFn: ({ signal }) => listKbCategories(signal),
  })
}

export function useKbArticles(params: KbArticleListParams) {
  return useQuery({
    queryKey: [...kbQueryKey, 'articles', params],
    queryFn: ({ signal }) => listKbArticles(params, signal),
  })
}

export function useKbFaqs() {
  return useQuery({
    queryKey: [...kbQueryKey, 'faqs'],
    queryFn: ({ signal }) => listKbFaqs(signal),
  })
}

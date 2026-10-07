import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ThumbsDownIcon, ThumbsUpIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { getSuggestions, sendSuggestionFeedback } from '@/api/ai'
import { linkTicketArticle } from '@/api/knowledge-base'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { usePermissions } from '@/features/auth/usePermissions'
import { aiQueryKey } from './useAi'

interface SuggestedSolutionsProps {
  ticketId: string
  /** Called with the text to append to the reply (title, summary and portal link of the article). */
  onInsert: (text: string) => void
}

/** The best published articles for the ticket (CRM-53): insert one into the reply, or say whether it was useful. */
export function SuggestedSolutions({ ticketId, onInsert }: SuggestedSolutionsProps) {
  const { t } = useTranslation()
  const { can } = usePermissions()
  const queryClient = useQueryClient()
  const allowed = can(permissions.kbView) && can(permissions.ticketsView)
  const key = [...aiQueryKey, 'suggestions', ticketId]
  const suggestions = useQuery({ queryKey: key, queryFn: ({ signal }) => getSuggestions(ticketId, signal), enabled: allowed })
  const vote = useMutation({
    mutationFn: ({ articleId, useful }: { articleId: string; useful: boolean }) => sendSuggestionFeedback(ticketId, articleId, useful),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: key }),
  })
  const insert = useMutation({
    mutationFn: (articleId: string) => linkTicketArticle(ticketId, articleId),
    onSuccess: (linked) => onInsert(linked.insertText),
  })

  if (!allowed || !suggestions.data || suggestions.data.length === 0) return null

  return (
    <section aria-label={t('ai.suggestions.title')} className="flex flex-col gap-2 rounded-lg border p-3">
      <h3 className="font-semibold">{t('ai.suggestions.title')}</h3>
      <ul className="flex flex-col gap-3">
        {suggestions.data.map((item) => (
          <li key={item.articleId} className="flex flex-col gap-1">
            <span className="font-medium">{item.title}</span>
            <span className="text-sm text-muted-foreground">{item.summary}</span>
            <div className="flex flex-wrap gap-2">
              <Button type="button" size="sm" disabled={insert.isPending} onClick={() => insert.mutate(item.articleId)}>
                {t('ai.suggestions.insert')}
              </Button>
              <Button
                type="button"
                size="sm"
                variant={item.useful === true ? 'default' : 'outline'}
                aria-pressed={item.useful === true}
                onClick={() => vote.mutate({ articleId: item.articleId, useful: true })}
              >
                <ThumbsUpIcon aria-hidden="true" />
                {t('ai.suggestions.useful')}
              </Button>
              <Button
                type="button"
                size="sm"
                variant={item.useful === false ? 'default' : 'outline'}
                aria-pressed={item.useful === false}
                onClick={() => vote.mutate({ articleId: item.articleId, useful: false })}
              >
                <ThumbsDownIcon aria-hidden="true" />
                {t('ai.suggestions.notUseful')}
              </Button>
            </div>
          </li>
        ))}
      </ul>
    </section>
  )
}

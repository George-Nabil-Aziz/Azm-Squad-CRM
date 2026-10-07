import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { isApiError } from '@/api/errors'
import { getPortalKbArticle, sendPortalKbFeedback } from '@/api/portal-kb'
import { Button } from '@/components/ui/button'

const VOTE_KEY_PREFIX = 'crm.portal-kb-vote.'

function readVote(id: string): 'yes' | 'no' | null {
  try {
    const value = localStorage.getItem(VOTE_KEY_PREFIX + id)
    return value === 'yes' || value === 'no' ? value : null
  } catch {
    return null
  }
}

function storeVote(id: string, vote: 'yes' | 'no') {
  try {
    localStorage.setItem(VOTE_KEY_PREFIX + id, vote)
  } catch {
    // Storage blocked: the buttons just stay enabled.
  }
}

/** A published help article (public) with the helpful buttons and counters. The browser remembers its own vote. */
export function PortalArticlePage() {
  const { t, i18n } = useTranslation()
  const { id = '' } = useParams()
  const queryClient = useQueryClient()
  const language = i18n.resolvedLanguage ?? i18n.language
  const key = ['portal-kb', language, 'article', id]
  const [vote, setVote] = useState(() => readVote(id))
  const article = useQuery({ queryKey: key, queryFn: ({ signal }) => getPortalKbArticle(id, signal), retry: false })
  const feedback = useMutation({
    mutationFn: (helpful: boolean) => sendPortalKbFeedback(id, helpful),
    onSuccess: async (_counts, helpful) => {
      const value = helpful ? 'yes' : 'no'
      storeVote(id, value)
      setVote(value)
      await queryClient.invalidateQueries({ queryKey: key })
    },
  })

  return (
    <article className="mx-auto flex w-full max-w-2xl flex-col gap-4">
      <Link to="/portal" className="w-fit text-sm text-primary underline-offset-4 hover:underline">
        {t('portal.article.back')}
      </Link>
      {article.isPending ? (
        <p className="text-muted-foreground">{t('portal.loading')}</p>
      ) : article.data ? (
        <>
          <p className="text-sm text-muted-foreground">{article.data.categoryName}</p>
          <h1 className="text-2xl font-semibold text-primary">{article.data.title}</h1>
          <div dir="auto" className="whitespace-pre-line wrap-break-word">
            {article.data.body}
          </div>
          <section aria-label={t('portal.article.helpfulQuestion')} className="flex flex-col gap-2 rounded-lg border p-4">
            <p className="font-medium">{t('portal.article.helpfulQuestion')}</p>
            {vote ? (
              <p role="status">{t('portal.article.thanks')}</p>
            ) : (
              <div className="flex gap-2">
                <Button variant="outline" disabled={feedback.isPending} onClick={() => feedback.mutate(true)}>
                  {t('portal.article.yes')}
                </Button>
                <Button variant="outline" disabled={feedback.isPending} onClick={() => feedback.mutate(false)}>
                  {t('portal.article.no')}
                </Button>
              </div>
            )}
            <p className="text-sm text-muted-foreground">
              {t('portal.article.counts', { yes: article.data.helpfulCount, no: article.data.notHelpfulCount })}
            </p>
          </section>
        </>
      ) : (
        <p className="text-muted-foreground">
          {isApiError(article.error) && article.error.status === 404 ? t('portal.article.notFound') : t('errors.generic')}
        </p>
      )}
    </article>
  )
}

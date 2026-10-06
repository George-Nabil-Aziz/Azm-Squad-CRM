import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { listTicketArticles } from '@/api/knowledge-base'
import { permissions } from '@/auth/permissions'
import { usePermissions } from '@/features/auth/usePermissions'
import { kbQueryKey } from './useKnowledgeBase'

/** The knowledge base articles agents inserted into replies of this ticket (nothing is shown when there are none). */
export function LinkedArticles({ ticketId }: { ticketId: string }) {
  const { t, i18n } = useTranslation()
  const { can } = usePermissions()
  const allowed = can(permissions.kbView)
  const articles = useQuery({
    queryKey: [...kbQueryKey, 'ticket-articles', ticketId],
    queryFn: ({ signal }) => listTicketArticles(ticketId, signal),
    enabled: allowed,
  })
  const formatTime = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' })

  if (!allowed || !articles.data || articles.data.length === 0) return null

  return (
    <section aria-label={t('knowledgeBase.linked.title')} className="flex flex-col gap-2">
      <h2 className="text-lg font-semibold">{t('knowledgeBase.linked.title')}</h2>
      <ul className="flex flex-col gap-1 text-sm">
        {articles.data.map((article) => (
          <li key={article.id}>
            <span className="font-medium">{article.title}</span>{' '}
            <span className="text-muted-foreground">
              <time dateTime={article.linkedAt}>{formatTime.format(new Date(article.linkedAt))}</time>
              {article.linkedByName ? ` · ${article.linkedByName}` : ''}
            </span>
          </li>
        ))}
      </ul>
    </section>
  )
}

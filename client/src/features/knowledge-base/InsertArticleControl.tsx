import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { BookOpenIcon, SearchIcon } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { linkTicketArticle, searchKb } from '@/api/knowledge-base'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { usePermissions } from '@/features/auth/usePermissions'
import { kbQueryKey } from './useKnowledgeBase'

interface InsertArticleControlProps {
  ticketId: string
  /** Called with the text to append to the reply (title, summary and portal link of the chosen article). */
  onInsert: (text: string) => void
}

/** Reply box helper: search the published articles, pick one, its link and summary go into the reply. */
export function InsertArticleControl({ ticketId, onInsert }: InsertArticleControlProps) {
  const { t } = useTranslation()
  const { can } = usePermissions()
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [text, setText] = useState('')
  const [query, setQuery] = useState('')
  const results = useQuery({
    queryKey: [...kbQueryKey, 'search', query],
    queryFn: ({ signal }) => searchKb(query, signal),
    enabled: open && query.length > 0,
  })
  const link = useMutation({
    mutationFn: (articleId: string) => linkTicketArticle(ticketId, articleId),
    onSuccess: async (linked) => {
      onInsert(linked.insertText)
      toast.success(t('knowledgeBase.insert.inserted', { title: linked.title }))
      setOpen(false)
      await queryClient.invalidateQueries({ queryKey: kbQueryKey })
    },
  })

  if (!can(permissions.kbView)) return null

  const articles = results.data?.filter((result) => result.type === 'article') ?? []

  return (
    <div className="flex flex-col gap-2">
      <div>
        <Button type="button" variant="outline" size="sm" aria-expanded={open} onClick={() => setOpen(!open)}>
          <BookOpenIcon aria-hidden="true" />
          {t('knowledgeBase.insert.button')}
        </Button>
      </div>
      {open ? (
        <div role="group" aria-label={t('knowledgeBase.insert.title')} className="flex flex-col gap-2 rounded-lg border p-3">
          <div className="flex gap-2">
            <Input
              type="search"
              aria-label={t('knowledgeBase.insert.searchLabel')}
              placeholder={t('knowledgeBase.insert.searchPlaceholder')}
              value={text}
              onChange={(event) => setText(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter') {
                  event.preventDefault()
                  setQuery(text.trim())
                }
              }}
            />
            <Button type="button" variant="outline" onClick={() => setQuery(text.trim())}>
              <SearchIcon aria-hidden="true" />
              {t('knowledgeBase.search.button')}
            </Button>
          </div>
          {query && results.data && articles.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('knowledgeBase.search.none')}</p>
          ) : null}
          <ul className="flex flex-col gap-2">
            {articles.map((article) => (
              <li key={article.id} className="flex items-start justify-between gap-3">
                <div className="flex flex-col">
                  <span className="font-medium">{article.title}</span>
                  <span className="text-sm text-muted-foreground">{article.snippet}</span>
                </div>
                <Button type="button" size="sm" disabled={link.isPending} onClick={() => link.mutate(article.id)}>
                  {t('knowledgeBase.insert.insert')}
                </Button>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  )
}

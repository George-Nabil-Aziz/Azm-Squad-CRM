import { useQuery } from '@tanstack/react-query'
import { SearchIcon } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { searchKb } from '@/api/knowledge-base'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { kbQueryKey } from './useKnowledgeBase'

/** Search box over published articles and FAQs, ranked by relevance. An empty box searches nothing. */
export function KbSearchPanel() {
  const { t } = useTranslation()
  const [text, setText] = useState('')
  const [query, setQuery] = useState('')
  const results = useQuery({
    queryKey: [...kbQueryKey, 'search', query],
    queryFn: ({ signal }) => searchKb(query, signal),
    enabled: query.length > 0,
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    setQuery(text.trim())
  }

  return (
    <section aria-label={t('knowledgeBase.search.title')} className="flex flex-col gap-3">
      <form role="search" onSubmit={submit} className="flex w-full max-w-xl gap-2">
        <Input
          type="search"
          aria-label={t('knowledgeBase.search.label')}
          placeholder={t('knowledgeBase.search.placeholder')}
          value={text}
          onChange={(event) => setText(event.target.value)}
        />
        <Button type="submit" variant="outline">
          <SearchIcon aria-hidden="true" />
          {t('knowledgeBase.search.button')}
        </Button>
      </form>
      {query && results.isPending ? <p className="text-muted-foreground">{t('knowledgeBase.loading')}</p> : null}
      {query && results.data && results.data.length === 0 ? (
        <p className="text-muted-foreground">{t('knowledgeBase.search.none')}</p>
      ) : null}
      {results.data && results.data.length > 0 ? (
        <ul className="flex flex-col gap-2">
          {results.data.map((result) => (
            <li key={`${result.type}-${result.id}`} className="flex flex-col gap-1 rounded-lg border p-3">
              <div className="flex items-center gap-2">
                <Badge variant="secondary">{t(`knowledgeBase.search.types.${result.type}`)}</Badge>
                <span className="font-medium">{result.title}</span>
              </div>
              <p className="text-sm text-muted-foreground">{result.snippet}</p>
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  )
}

import { useQuery } from '@tanstack/react-query'
import { SearchIcon } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { listPortalFaqs, listPortalKbArticles, listPortalKbCategories, searchPortalKb } from '@/api/portal-kb'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

/** Public help center: search, FAQs and published articles by category. No sign-in needed; content follows the UI language. */
export function PortalHomePage() {
  const { t, i18n } = useTranslation()
  const language = i18n.resolvedLanguage ?? i18n.language
  const [text, setText] = useState('')
  const [query, setQuery] = useState('')
  const [categoryId, setCategoryId] = useState('')
  // The language is part of the keys: the server answers in it, so a language switch reloads the content.
  const faqs = useQuery({ queryKey: ['portal-kb', language, 'faqs'], queryFn: ({ signal }) => listPortalFaqs(signal) })
  const categories = useQuery({ queryKey: ['portal-kb', language, 'categories'], queryFn: ({ signal }) => listPortalKbCategories(signal) })
  const articles = useQuery({
    queryKey: ['portal-kb', language, 'articles', categoryId],
    queryFn: ({ signal }) => listPortalKbArticles(categoryId, signal),
  })
  const results = useQuery({
    queryKey: ['portal-kb', language, 'search', query],
    queryFn: ({ signal }) => searchPortalKb(query, signal),
    enabled: query.length > 0,
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    setQuery(text.trim())
  }

  return (
    <div className="flex flex-col gap-8">
      <div className="flex flex-col gap-2">
        <h1 className="text-2xl font-semibold">{t('portal.home.title')}</h1>
        <p className="text-muted-foreground">{t('portal.home.description')}</p>
        <form role="search" onSubmit={submit} className="flex gap-2">
          <Input
            type="search"
            aria-label={t('portal.home.searchLabel')}
            placeholder={t('portal.home.searchPlaceholder')}
            value={text}
            onChange={(event) => setText(event.target.value)}
          />
          <Button type="submit">
            <SearchIcon aria-hidden="true" />
            {t('portal.home.search')}
          </Button>
        </form>
      </div>

      {query ? (
        <section aria-label={t('portal.home.results')} className="flex flex-col gap-2">
          <h2 className="text-lg font-semibold">{t('portal.home.results')}</h2>
          {results.data && results.data.length === 0 ? <p className="text-muted-foreground">{t('portal.home.noResults')}</p> : null}
          <ul className="flex flex-col gap-2">
            {results.data?.map((hit) =>
              hit.type === 'article' ? (
                <li key={`a-${hit.id}`} className="rounded-lg border p-3">
                  <Link to={`/portal/kb/articles/${hit.id}`} className="font-medium text-primary underline-offset-4 hover:underline">
                    {hit.title}
                  </Link>
                  <p className="text-sm text-muted-foreground">{hit.snippet}</p>
                </li>
              ) : (
                <li key={`f-${hit.id}`} className="rounded-lg border p-3">
                  <p className="font-medium">{hit.title}</p>
                  <p className="text-sm text-muted-foreground">{hit.snippet}</p>
                </li>
              ),
            )}
          </ul>
        </section>
      ) : null}

      <section aria-label={t('portal.home.faqs')} className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold">{t('portal.home.faqs')}</h2>
        {faqs.data && faqs.data.length === 0 ? <p className="text-muted-foreground">{t('portal.home.noFaqs')}</p> : null}
        {faqs.data?.map((faq) => (
          <details key={faq.id} className="rounded-lg border p-3">
            <summary className="cursor-pointer font-medium">{faq.question}</summary>
            <p dir="auto" className="mt-2 whitespace-pre-line text-muted-foreground">
              {faq.answer}
            </p>
          </details>
        ))}
      </section>

      <section aria-label={t('portal.home.articles')} className="flex flex-col gap-3">
        <h2 className="text-lg font-semibold">{t('portal.home.articles')}</h2>
        {categories.data && categories.data.length > 0 ? (
          <div className="flex flex-wrap gap-2" role="group" aria-label={t('portal.home.categories')}>
            <Button size="sm" variant={categoryId === '' ? 'secondary' : 'outline'} aria-pressed={categoryId === ''} onClick={() => setCategoryId('')}>
              {t('portal.home.allCategories')}
            </Button>
            {categories.data.map((category) => (
              <Button
                key={category.id}
                size="sm"
                variant={categoryId === category.id ? 'secondary' : 'outline'}
                aria-pressed={categoryId === category.id}
                onClick={() => setCategoryId(category.id)}
              >
                {category.name} ({category.articleCount})
              </Button>
            ))}
          </div>
        ) : null}
        {articles.data && articles.data.items.length === 0 ? <p className="text-muted-foreground">{t('portal.home.noArticles')}</p> : null}
        <ul className="flex flex-col gap-2">
          {articles.data?.items.map((article) => (
            <li key={article.id} className="rounded-lg border p-3">
              <Link to={`/portal/kb/articles/${article.id}`} className="font-medium text-primary underline-offset-4 hover:underline">
                {article.title}
              </Link>
              <p className="text-sm text-muted-foreground">{article.summary}</p>
            </li>
          ))}
        </ul>
      </section>
    </div>
  )
}

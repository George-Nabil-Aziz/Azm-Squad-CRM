import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { ArticlesPanel } from '@/features/knowledge-base/ArticlesPanel'
import { CategoriesPanel } from '@/features/knowledge-base/CategoriesPanel'
import { FaqsPanel } from '@/features/knowledge-base/FaqsPanel'
import { KbSearchPanel } from '@/features/knowledge-base/KbSearchPanel'

const tabs = ['articles', 'faqs', 'categories'] as const
type Tab = (typeof tabs)[number]

const panels = { articles: ArticlesPanel, faqs: FaqsPanel, categories: CategoriesPanel } as const

/** Knowledge base area: Articles, FAQs and Categories tabs. */
export function KnowledgeBasePage() {
  const { t } = useTranslation()
  const [tab, setTab] = useState<Tab>('articles')
  const Panel = panels[tab]

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold text-primary">{t('nav.knowledgeBase')}</h1>
        <p className="text-muted-foreground">{t('knowledgeBase.description')}</p>
      </div>

      <KbSearchPanel />

      <div role="tablist" aria-label={t('knowledgeBase.sections')} className="flex gap-2">
        {tabs.map((id) => (
          <Button
            key={id}
            role="tab"
            id={`kb-tab-${id}`}
            aria-selected={tab === id}
            aria-controls="kb-panel"
            variant={tab === id ? 'secondary' : 'ghost'}
            onClick={() => setTab(id)}
          >
            {t(`knowledgeBase.tabs.${id}`)}
          </Button>
        ))}
      </div>

      <div role="tabpanel" id="kb-panel" aria-labelledby={`kb-tab-${tab}`}>
        <Panel />
      </div>
    </div>
  )
}

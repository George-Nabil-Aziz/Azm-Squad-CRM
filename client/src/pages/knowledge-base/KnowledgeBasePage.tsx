import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { ArticlesPanel } from '@/features/knowledge-base/ArticlesPanel'
import { CategoriesPanel } from '@/features/knowledge-base/CategoriesPanel'

const tabs = ['articles', 'categories'] as const
type Tab = (typeof tabs)[number]

/** Knowledge base area: Articles and Categories tabs (FAQs are added by CRM-37). */
export function KnowledgeBasePage() {
  const { t } = useTranslation()
  const [tab, setTab] = useState<Tab>('articles')

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold">{t('nav.knowledgeBase')}</h1>
        <p className="text-muted-foreground">{t('knowledgeBase.description')}</p>
      </div>

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
        {tab === 'articles' ? <ArticlesPanel /> : <CategoriesPanel />}
      </div>
    </div>
  )
}

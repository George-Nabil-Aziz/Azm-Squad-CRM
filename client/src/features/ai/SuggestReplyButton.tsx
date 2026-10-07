import { useMutation } from '@tanstack/react-query'
import { SparklesIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { generateReplyDraft } from '@/api/ai'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { usePermissions } from '@/features/auth/usePermissions'
import { useAiStatus } from './useAi'

interface SuggestReplyButtonProps {
  ticketId: string
  /** Called with the draft text; the reply box shows it for review (nothing is sent). */
  onDraft: (draft: string) => void
}

/** Reply box helper (CRM-51): asks the AI for a draft from the thread and the knowledge base. Hidden while AI is off. */
export function SuggestReplyButton({ ticketId, onDraft }: SuggestReplyButtonProps) {
  const { t } = useTranslation()
  const { can } = usePermissions()
  const status = useAiStatus()
  const suggest = useMutation({
    mutationFn: () => generateReplyDraft(ticketId),
    onSuccess: (result) => onDraft(result.draft),
  })

  if (status.data?.enabled !== true || !can(permissions.ticketsManage)) return null

  const articles = suggest.data?.articles ?? []

  return (
    <div className="flex flex-col gap-2">
      <div>
        <Button type="button" variant="outline" size="sm" disabled={suggest.isPending} onClick={() => suggest.mutate()}>
          <SparklesIcon aria-hidden="true" />
          {suggest.isPending ? t('ai.reply.suggesting') : t('ai.reply.suggest')}
        </Button>
      </div>
      {suggest.isSuccess ? (
        <div className="flex flex-col gap-1 text-sm text-muted-foreground">
          <p>{t('ai.reply.review')}</p>
          {articles.length > 0 ? (
            <div>
              <span>{t('ai.reply.basedOn')}</span>
              <ul className="list-inside list-disc">
                {articles.map((article) => (
                  <li key={article.id}>{article.title}</li>
                ))}
              </ul>
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  )
}

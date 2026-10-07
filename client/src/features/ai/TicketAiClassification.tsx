import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { getTicketClassification } from '@/api/ai'
import { changeTicketCategory, changeTicketPriority } from '@/api/tickets'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { usePermissions } from '@/features/auth/usePermissions'
import { ticketsQueryKey } from '@/features/tickets/useTickets'
import { aiQueryKey } from './useAi'

/**
 * What the AI suggested when the ticket was created (CRM-52): applied automatically (confidence above the threshold) or
 * only a suggestion the agent can apply with one click. Nothing is shown for a ticket without a classification.
 */
export function TicketAiClassification({ ticketId }: { ticketId: string }) {
  const { t } = useTranslation()
  const { can } = usePermissions()
  const queryClient = useQueryClient()
  const key = [...aiQueryKey, 'classification', ticketId]
  const result = useQuery({
    queryKey: key,
    queryFn: ({ signal }) => getTicketClassification(ticketId, signal),
    enabled: can(permissions.ticketsView),
  })
  const classification = result.data
  const apply = useMutation({
    mutationFn: async () => {
      if (!classification?.suggestedPriority) return
      if (classification.suggestedCategoryId) await changeTicketCategory(ticketId, classification.suggestedCategoryId)
      await changeTicketPriority(ticketId, classification.suggestedPriority)
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ticketsQueryKey })
      await queryClient.invalidateQueries({ queryKey: key })
    },
  })

  if (!classification || classification.status === 'none') return null

  const overridden = classification.categoryOverriddenAt !== null || classification.priorityOverriddenAt !== null

  return (
    <section aria-label={t('ai.classification.title')} className="flex flex-col gap-1 rounded-lg border p-3 text-sm">
      <h2 className="font-semibold">{t('ai.classification.title')}</h2>
      <p>
        {t('ai.classification.suggested', {
          category: classification.suggestedCategoryName ?? t('tickets.noCategory'),
          priority: classification.suggestedPriority ? t(`tickets.priorities.${classification.suggestedPriority}`) : '',
          confidence: Math.round((classification.confidence ?? 0) * 100),
        })}
      </p>
      <p className="text-muted-foreground">
        {classification.status === 'applied' ? t('ai.classification.applied') : t('ai.classification.suggestionOnly')}
      </p>
      {overridden ? <p className="text-muted-foreground">{t('ai.classification.overridden')}</p> : null}
      {classification.status === 'suggested' && can(permissions.ticketsManage) ? (
        <div>
          <Button type="button" size="sm" variant="outline" disabled={apply.isPending} onClick={() => apply.mutate()}>
            {t('ai.classification.apply')}
          </Button>
        </div>
      ) : null}
    </section>
  )
}

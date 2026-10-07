import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { SparklesIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { generateTicketSummary, getTicketSummary } from '@/api/ai'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { usePermissions } from '@/features/auth/usePermissions'
import { aiQueryKey, useAiStatus } from './useAi'

/**
 * One-click AI summary of the ticket thread (CRM-50). Hidden while AI is not configured; the summary is saved on the server
 * with its time, and regenerating replaces it. A failed call shows the error toast and keeps the old summary.
 */
export function TicketSummary({ ticketId }: { ticketId: string }) {
  const { t, i18n } = useTranslation()
  const { can } = usePermissions()
  const queryClient = useQueryClient()
  const status = useAiStatus()
  const enabled = status.data?.enabled === true && can(permissions.ticketsView)
  const key = [...aiQueryKey, 'summary', ticketId]
  const summary = useQuery({ queryKey: key, queryFn: ({ signal }) => getTicketSummary(ticketId, signal), enabled })
  const generate = useMutation({
    mutationFn: () => generateTicketSummary(ticketId),
    onSuccess: (generated) => queryClient.setQueryData(key, generated),
  })

  if (!enabled) return null

  const saved = summary.data
  const formatTime = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' })

  return (
    <section aria-label={t('ai.summary.title')} className="flex flex-col gap-2 rounded-lg border p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-semibold text-primary">{t('ai.summary.title')}</h2>
        {can(permissions.ticketsManage) ? (
          <Button type="button" variant="outline" size="sm" disabled={generate.isPending} onClick={() => generate.mutate()}>
            <SparklesIcon aria-hidden="true" />
            {generate.isPending
              ? t('ai.summary.generating')
              : saved?.text
                ? t('ai.summary.regenerate')
                : t('ai.summary.generate')}
          </Button>
        ) : null}
      </div>
      {saved?.text ? (
        <>
          <p dir={saved.language === 'ar' ? 'rtl' : 'ltr'} className="whitespace-pre-line wrap-break-word">
            {saved.text}
          </p>
          {saved.generatedAt ? (
            <p className="text-sm text-muted-foreground">
              {t('ai.summary.generatedAt', { time: formatTime.format(new Date(saved.generatedAt)) })}
            </p>
          ) : null}
        </>
      ) : (
        <p className="text-sm text-muted-foreground">{t('ai.summary.empty')}</p>
      )}
    </section>
  )
}

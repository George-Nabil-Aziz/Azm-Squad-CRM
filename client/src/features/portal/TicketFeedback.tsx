import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { getPortalTicketFeedback, submitPortalTicketFeedback } from '@/api/portal'
import { SurveyView } from './SurveyView'

/** The satisfaction survey of a resolved ticket inside the portal (nothing is shown while the ticket has no survey). */
export function TicketFeedback({ ticketId, status }: { ticketId: string; status: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const key = ['portal', 'feedback', ticketId, status]
  const survey = useQuery({ queryKey: key, queryFn: ({ signal }) => getPortalTicketFeedback(ticketId, signal) })
  const submit = useMutation({
    mutationFn: (answer: { rating: number; comment: string }) => submitPortalTicketFeedback(ticketId, answer),
    onSuccess: (saved) => queryClient.setQueryData(key, saved),
  })

  if (!survey.data || survey.data.state === 'none') return null

  return (
    <section aria-label={t('portal.survey.title')} className="flex flex-col gap-2 rounded-lg border p-4">
      <h2 className="text-lg font-semibold text-primary">{t('portal.survey.title')}</h2>
      <SurveyView survey={survey.data} submit={(answer) => submit.mutateAsync(answer)} />
    </section>
  )
}

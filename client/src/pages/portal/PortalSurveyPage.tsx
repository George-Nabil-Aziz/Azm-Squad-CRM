import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { isApiError } from '@/api/errors'
import { getPortalSurvey, submitPortalSurvey } from '@/api/portal'
import { SurveyView } from '@/features/portal/SurveyView'

/** The satisfaction survey behind the emailed link (public: the token in the link is the credential). */
export function PortalSurveyPage() {
  const { t } = useTranslation()
  const { token = '' } = useParams()
  const queryClient = useQueryClient()
  const key = ['portal', 'survey', token]
  const survey = useQuery({ queryKey: key, queryFn: ({ signal }) => getPortalSurvey(token, signal), retry: false })
  const submit = useMutation({
    mutationFn: (answer: { rating: number; comment: string }) => submitPortalSurvey(token, answer),
    onSuccess: (saved) => queryClient.setQueryData(key, saved),
  })

  return (
    <div className="mx-auto flex w-full max-w-xl flex-col gap-4">
      <h1 className="text-2xl font-semibold">{t('portal.survey.title')}</h1>
      {survey.isPending ? (
        <p className="text-muted-foreground">{t('portal.loading')}</p>
      ) : survey.data ? (
        <>
          <p className="text-muted-foreground">
            <span dir="ltr">{survey.data.ticketNumber}</span> · {survey.data.subject}
          </p>
          <SurveyView survey={survey.data} submit={(answer) => submit.mutateAsync(answer)} />
        </>
      ) : (
        <p className="text-muted-foreground">
          {isApiError(survey.error) && survey.error.status === 404 ? t('portal.survey.notFound') : t('errors.generic')}
        </p>
      )}
      <Link to="/portal" className="w-fit text-sm text-primary underline-offset-4 hover:underline">
        {t('portal.article.back')}
      </Link>
    </div>
  )
}

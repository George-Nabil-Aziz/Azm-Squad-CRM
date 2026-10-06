import { useTranslation } from 'react-i18next'
import { isApiError } from '@/api/errors'
import type { PortalSurvey } from '@/api/portal'
import { RatingForm, type RatingAnswer } from './RatingForm'

interface SurveyViewProps {
  survey: PortalSurvey
  /** Sends the answer; rejects with the ApiError of a failed call. */
  submit(answer: RatingAnswer): PromiseLike<unknown>
}

/** The survey in its state: the form while it is open, the saved answer, or the expired notice (nothing when there is no survey). */
export function SurveyView({ survey, submit }: SurveyViewProps) {
  const { t } = useTranslation()

  if (survey.state === 'none') return null
  if (survey.state === 'answered') {
    return (
      <div role="status" className="flex flex-col gap-1">
        <p className="font-medium">{t('portal.survey.thanks')}</p>
        <p className="text-sm text-muted-foreground">{t('portal.survey.yourRating', { count: survey.rating ?? 0 })}</p>
        {survey.comment ? <p dir="auto" className="whitespace-pre-line text-sm">{survey.comment}</p> : null}
      </div>
    )
  }
  if (survey.state === 'expired') {
    return <p role="status">{t('portal.survey.expired')}</p>
  }

  return (
    <RatingForm
      onSubmit={async (answer) => {
        try {
          await submit(answer)
          return undefined
        } catch (caught) {
          // 400: the server's message (already in the UI language); every failure also toasts.
          const errors = isApiError(caught) && caught.status === 400 ? caught.problem?.errors : undefined
          return errors?.rating?.[0] ?? errors?.token?.[0] ?? errors?.comment?.[0]
        }
      }}
    />
  )
}

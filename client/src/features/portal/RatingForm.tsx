import { StarIcon } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Field, FieldError, FieldLabel } from '@/components/ui/field'
import { Textarea } from '@/components/ui/textarea'

interface RatingFormProps {
  /** Saves the answer; resolves with an error message to show (a 400 of the server), or nothing on success. */
  onSubmit(answer: RatingAnswer): SavePromise
}

/** The answer of the form. */
export interface RatingAnswer {
  rating: number
  comment: string
}

/** Result of saving: the server's error message to show, or nothing. */
type SavePromise = PromiseLike<string | undefined>

const STARS = [1, 2, 3, 4, 5] as const

/** 1-5 star rating with an optional comment. */
export function RatingForm({ onSubmit }: RatingFormProps) {
  const { t } = useTranslation()
  const [rating, setRating] = useState(0)
  const [comment, setComment] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (rating === 0) {
      setError(t('portal.survey.ratingRequired'))
      return
    }
    setBusy(true)
    const message = await onSubmit({ rating, comment: comment.trim() })
    setBusy(false)
    setError(message ?? null)
  }

  return (
    <form noValidate onSubmit={submit} aria-label={t('portal.survey.form')} className="flex flex-col gap-3">
      <fieldset className="flex flex-col gap-2">
        <legend className="font-medium">{t('portal.survey.question')}</legend>
        <div className="flex gap-1" role="radiogroup" aria-label={t('portal.survey.question')}>
          {STARS.map((value) => (
            <Button
              key={value}
              type="button"
              role="radio"
              aria-checked={rating === value}
              aria-label={t('portal.survey.stars', { count: value })}
              variant={rating >= value ? 'secondary' : 'outline'}
              size="icon"
              onClick={() => setRating(value)}
            >
              <StarIcon aria-hidden="true" className={rating >= value ? 'fill-current' : ''} />
            </Button>
          ))}
        </div>
      </fieldset>
      <Field data-invalid={error !== null}>
        <FieldLabel htmlFor="survey-comment">{t('portal.survey.comment')}</FieldLabel>
        <Textarea id="survey-comment" dir="auto" rows={3} value={comment} maxLength={2000} onChange={(event) => setComment(event.target.value)} />
        {error ? <FieldError errors={[{ message: error }]} /> : null}
      </Field>
      <div>
        <Button type="submit" disabled={busy}>
          {t('portal.survey.send')}
        </Button>
      </div>
    </form>
  )
}

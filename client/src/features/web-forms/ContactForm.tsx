import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useCallback, useMemo, useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { isApiError } from '@/api/errors'
import { getWebFormConfig, submitWebForm, type WebFormReceipt, type WebFormSubmit } from '@/api/web-forms'
import { Button } from '@/components/ui/button'
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { CaptchaWidget } from './CaptchaWidget'
import { contactFormFields, createContactFormSchema, type ContactFormValues } from './contact-form-schema'

const autoCompleteOf: Record<(typeof contactFormFields)[number], string> = {
  name: 'name',
  email: 'email',
  subject: 'off',
  message: 'off',
}

/** The public contact form: on success `onSubmitted` gets the receipt (ticket number). */
export function ContactForm({
  onSubmitted,
  submit: send = submitWebForm,
}: {
  onSubmitted: (receipt: WebFormReceipt) => void
  /** Where the form goes (default: the web form endpoint; the chat widget's offline form passes its own). */
  submit?: WebFormSubmit
}) {
  const { t } = useTranslation()
  const schema = useMemo(() => createContactFormSchema(t), [t])
  const [captchaToken, setCaptchaToken] = useState('')
  const [honeypot, setHoneypot] = useState('')
  const [formError, setFormError] = useState<string | null>(null)
  const config = useQuery({ queryKey: ['web-forms', 'config'], queryFn: ({ signal }) => getWebFormConfig(signal) })
  const form = useForm<ContactFormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: '', email: '', subject: '', message: '' },
  })
  const submit = useMutation({
    mutationFn: (values: ContactFormValues) => send({ ...values, captchaToken, website: honeypot }),
    onSuccess: onSubmitted,
  })
  const onToken = useCallback((token: string) => setCaptchaToken(token), [])

  async function onSubmit(values: ContactFormValues) {
    setFormError(null)
    if (config.data?.captchaRequired && !captchaToken) {
      setFormError(t('webForms.form.captchaRequired'))
      return
    }
    try {
      await submit.mutateAsync(values)
    } catch (caught) {
      if (!isApiError(caught)) return
      if (caught.status === 429) {
        setFormError(t('webForms.form.tooManyRequests'))
        return
      }
      if (caught.status !== 400) return
      const errors = caught.problem?.errors
      for (const field of contactFormFields) {
        const message = errors?.[field]?.[0]
        if (message) form.setError(field, { message })
      }
      if (errors?.captchaToken?.[0]) setFormError(errors.captchaToken[0])
    }
  }

  return (
    <form aria-label={t('webForms.form.title')} noValidate onSubmit={form.handleSubmit(onSubmit)}>
      <FieldGroup>
        {contactFormFields.map((name) => (
          <Controller
            key={name}
            name={name}
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor={`web-form-${name}`}>{t(`webForms.form.${name}`)}</FieldLabel>
                {name === 'message' ? (
                  <Textarea {...field} id={`web-form-${name}`} rows={6} dir="auto" aria-invalid={fieldState.invalid} />
                ) : (
                  <Input
                    {...field}
                    id={`web-form-${name}`}
                    type={name === 'email' ? 'email' : 'text'}
                    dir="auto"
                    autoComplete={autoCompleteOf[name]}
                    aria-invalid={fieldState.invalid}
                  />
                )}
                {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
              </Field>
            )}
          />
        ))}
        {/* Honeypot: people never see or fill it; bots do. */}
        <div aria-hidden="true" className="absolute -start-[9999px] h-0 w-0 overflow-hidden">
          <label>
            {t('webForms.form.honeypot')}
            <input tabIndex={-1} autoComplete="off" value={honeypot} onChange={(event) => setHoneypot(event.target.value)} />
          </label>
        </div>
        {config.data?.captchaRequired && config.data.captchaSiteKey ? (
          <CaptchaWidget siteKey={config.data.captchaSiteKey} onToken={onToken} />
        ) : null}
        {formError ? (
          <p role="alert" className="text-sm text-destructive">
            {formError}
          </p>
        ) : null}
        <Button type="submit" disabled={form.formState.isSubmitting}>
          {form.formState.isSubmitting ? t('webForms.form.sending') : t('webForms.form.send')}
        </Button>
      </FieldGroup>
    </form>
  )
}

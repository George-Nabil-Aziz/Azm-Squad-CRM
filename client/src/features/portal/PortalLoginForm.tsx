import { zodResolver } from '@hookform/resolvers/zod'
import { useMemo, useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { isApiError } from '@/api/errors'
import { requestPortalCode, verifyPortalCode } from '@/api/portal'
import { savePortalSession } from '@/auth/portal-session'
import { Button } from '@/components/ui/button'
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { DemoAccounts } from '@/features/auth/DemoAccounts'
import { createPortalCodeSchema, createPortalEmailSchema, type PortalCodeValues, type PortalEmailValues } from './portal-login-schema'

function EmailStep({ onSent }: { onSent: (email: string) => void }) {
  const { t } = useTranslation()
  const schema = useMemo(() => createPortalEmailSchema(t), [t])
  const form = useForm<PortalEmailValues>({ resolver: zodResolver(schema), defaultValues: { email: '' } })

  async function onSubmit(values: PortalEmailValues) {
    try {
      await requestPortalCode(values.email)
      onSent(values.email)
    } catch (caught) {
      // 400 (not an email address): the server's message next to the field; every failure also toasts.
      const message = isApiError(caught) && caught.status === 400 ? caught.problem?.errors?.email?.[0] : undefined
      if (message) form.setError('email', { message })
    }
  }

  return (
    <>
      <form aria-label={t('portal.login.emailStep')} noValidate onSubmit={form.handleSubmit(onSubmit)}>
        <FieldGroup>
          <Controller
            name="email"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="portal-email">{t('portal.login.email')}</FieldLabel>
                <Input {...field} id="portal-email" type="email" autoComplete="email" dir="ltr" aria-invalid={fieldState.invalid} />
                <FieldDescription>{t('portal.login.emailHint')}</FieldDescription>
                {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
              </Field>
            )}
          />
          <Button type="submit" className="w-full" disabled={form.formState.isSubmitting}>
            {form.formState.isSubmitting ? t('portal.login.sending') : t('portal.login.sendCode')}
          </Button>
        </FieldGroup>
      </form>
      <DemoAccounts audience="customer" onPick={(email) => form.setValue('email', email, { shouldValidate: form.formState.isSubmitted })} />
    </>
  )
}

function CodeStep({ email, onBack, onSignedIn }: { email: string; onBack: () => void; onSignedIn: () => void }) {
  const { t } = useTranslation()
  const [rejected, setRejected] = useState(false)
  const schema = useMemo(() => createPortalCodeSchema(t), [t])
  const form = useForm<PortalCodeValues>({ resolver: zodResolver(schema), defaultValues: { code: '' } })

  async function onSubmit(values: PortalCodeValues) {
    setRejected(false)
    try {
      const login = await verifyPortalCode(email, values.code)
      savePortalSession(login.accessToken, login.expiresAt, login.customer)
      onSignedIn()
    } catch (caught) {
      // A wrong, expired or used code is shown here; every other failure already raised a toast.
      if (isApiError(caught) && caught.status === 401) setRejected(true)
    }
  }

  async function resend() {
    await requestPortalCode(email)
    toast.success(t('portal.login.codeResent', { email }))
  }

  return (
    <form aria-label={t('portal.login.codeStep')} noValidate onSubmit={form.handleSubmit(onSubmit)}>
      <FieldGroup>
        <p className="text-sm text-muted-foreground">{t('portal.login.codeSent', { email })}</p>
        <Controller
          name="code"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="portal-code">{t('portal.login.code')}</FieldLabel>
              <Input
                {...field}
                id="portal-code"
                inputMode="numeric"
                autoComplete="one-time-code"
                maxLength={6}
                dir="ltr"
                aria-invalid={fieldState.invalid}
              />
              {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
            </Field>
          )}
        />
        {rejected ? <FieldError>{t('portal.login.codeRejected')}</FieldError> : null}
        <Button type="submit" className="w-full" disabled={form.formState.isSubmitting}>
          {form.formState.isSubmitting ? t('portal.login.verifying') : t('portal.login.verify')}
        </Button>
        <div className="flex justify-between gap-2">
          <Button type="button" variant="ghost" size="sm" onClick={onBack}>
            {t('portal.login.changeEmail')}
          </Button>
          <Button type="button" variant="ghost" size="sm" onClick={() => void resend()}>
            {t('portal.login.resend')}
          </Button>
        </div>
      </FieldGroup>
    </form>
  )
}

/** Two steps: the email address (a code is mailed), then the code. On success the portal session changes and the page redirects. */
export function PortalLoginForm({ onSignedIn }: { onSignedIn: () => void }) {
  const [email, setEmail] = useState<string | null>(null)

  return email === null ? <EmailStep onSent={setEmail} /> : <CodeStep email={email} onBack={() => setEmail(null)} onSignedIn={onSignedIn} />
}

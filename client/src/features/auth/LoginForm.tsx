import { zodResolver } from '@hookform/resolvers/zod'
import { useMemo, useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { isApiError } from '@/api/errors'
import { signIn } from '@/auth/sign-in'
import { Button } from '@/components/ui/button'
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { DemoAccounts } from './DemoAccounts'
import { createLoginSchema, type LoginValues } from './login-schema'

/** Email + password form (react-hook-form + zod). On success the session changes and LoginPage redirects. */
export function LoginForm() {
  const { t } = useTranslation()
  const [invalidCredentials, setInvalidCredentials] = useState(false)
  // `t` changes with the language, so the next validation uses messages in the new language.
  const schema = useMemo(() => createLoginSchema(t), [t])
  const form = useForm<LoginValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '' },
  })

  async function onSubmit(values: LoginValues) {
    setInvalidCredentials(false)
    try {
      await signIn(values.email, values.password)
    } catch (caught) {
      // Wrong credentials are shown here; every other failure already raised a toast (ApiErrorToaster).
      if (isApiError(caught) && caught.status === 401) setInvalidCredentials(true)
    }
  }

  const isSubmitting = form.formState.isSubmitting

  return (
    <>
      <form aria-label={t('auth.signInTitle')} noValidate onSubmit={form.handleSubmit(onSubmit)}>
        <FieldGroup>
          <Controller
            name="email"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="login-email">{t('auth.email')}</FieldLabel>
                <Input {...field} id="login-email" type="email" autoComplete="username" aria-invalid={fieldState.invalid} />
                {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
              </Field>
            )}
          />
          <Controller
            name="password"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="login-password">{t('auth.password')}</FieldLabel>
                <Input {...field} id="login-password" type="password" autoComplete="current-password" aria-invalid={fieldState.invalid} />
                {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
              </Field>
            )}
          />
          {invalidCredentials ? <FieldError>{t('auth.invalidCredentials')}</FieldError> : null}
          <Button type="submit" className="w-full" disabled={isSubmitting}>
            {isSubmitting ? t('auth.signingIn') : t('auth.signIn')}
          </Button>
        </FieldGroup>
      </form>
      <DemoAccounts
        audience="staff"
        onPick={(account) => {
          form.setValue('email', account.email, { shouldValidate: form.formState.isSubmitted })
          form.setValue('password', account.password ?? '', { shouldValidate: form.formState.isSubmitted })
          form.setFocus(account.password ? 'email' : 'password')
        }}
      />
    </>
  )
}

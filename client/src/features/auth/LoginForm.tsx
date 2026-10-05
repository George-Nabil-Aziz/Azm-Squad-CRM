import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { isApiError } from '@/api/errors'
import { signIn } from '@/auth/sign-in'
import { Button } from '@/components/ui/button'
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { authMessages } from './auth-messages'
import { loginSchema, type LoginValues } from './login-schema'

/** Email + password form (react-hook-form + zod). On success the session changes and LoginPage redirects. */
export function LoginForm() {
  const [serverError, setServerError] = useState<string | null>(null)
  const form = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  })

  async function onSubmit(values: LoginValues) {
    setServerError(null)
    try {
      await signIn(values.email, values.password)
    } catch (caught) {
      // Wrong credentials are shown here; every other failure already raised a toast (ApiErrorToaster).
      if (isApiError(caught) && caught.status === 401) setServerError(authMessages.invalidCredentials)
    }
  }

  const isSubmitting = form.formState.isSubmitting

  return (
    <form aria-label={authMessages.signInTitle} noValidate onSubmit={form.handleSubmit(onSubmit)}>
      <FieldGroup>
        <Controller
          name="email"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="login-email">{authMessages.email}</FieldLabel>
              <Input
                {...field}
                id="login-email"
                type="email"
                autoComplete="username"
                aria-invalid={fieldState.invalid}
              />
              {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
            </Field>
          )}
        />
        <Controller
          name="password"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="login-password">{authMessages.password}</FieldLabel>
              <Input
                {...field}
                id="login-password"
                type="password"
                autoComplete="current-password"
                aria-invalid={fieldState.invalid}
              />
              {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
            </Field>
          )}
        />
        {serverError ? <FieldError>{serverError}</FieldError> : null}
        <Button type="submit" className="w-full" disabled={isSubmitting}>
          {isSubmitting ? authMessages.signingIn : authMessages.signIn}
        </Button>
      </FieldGroup>
    </form>
  )
}

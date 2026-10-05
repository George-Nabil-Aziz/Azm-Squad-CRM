import { useState, type FormEvent } from 'react'
import { isApiError } from '../../api/errors'
import { signIn } from '../../auth/sign-in'
import { authMessages } from './auth-messages'

/** Minimal sign-in form. CRM-3 replaces it with the styled login page (shadcn/ui, react-hook-form + zod). */
export function LoginForm() {
  const [error, setError] = useState<string | null>(null)
  const [isPending, setIsPending] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    setError(null)
    setIsPending(true)
    try {
      await signIn(String(form.get('email') ?? ''), String(form.get('password') ?? ''))
      // Success: the session store changes and App renders the signed-in view instead of this form.
    } catch (caught) {
      // Wrong credentials are shown here; every other failure already raised a toast (ApiErrorToaster).
      if (isApiError(caught) && caught.status === 401) setError(authMessages.invalidCredentials)
      setIsPending(false)
    }
  }

  return (
    <form aria-labelledby="sign-in-title" onSubmit={handleSubmit}>
      <h2 id="sign-in-title">{authMessages.signInTitle}</h2>
      <label>
        {authMessages.email}
        <input name="email" type="email" autoComplete="username" required />
      </label>
      <label>
        {authMessages.password}
        <input name="password" type="password" autoComplete="current-password" required />
      </label>
      {error ? <p role="alert">{error}</p> : null}
      <button type="submit" disabled={isPending}>
        {isPending ? authMessages.signingIn : authMessages.signIn}
      </button>
    </form>
  )
}

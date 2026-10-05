import { useEffect, useState } from 'react'
import { getCurrentUser, type CurrentUser } from '../../api/auth'
import { signOut } from '../../auth/sign-in'
import { authMessages } from './auth-messages'

/** Shows who is signed in (GET /api/auth/me, a protected endpoint) and a sign-out button. */
export function CurrentUserPanel() {
  const [user, setUser] = useState<CurrentUser | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    getCurrentUser(controller.signal)
      .then(setUser)
      // 401 already cleared the session (App shows the sign-in form); other failures raised a toast.
      .catch(() => {})
    return () => controller.abort()
  }, [])

  return (
    <section aria-label={authMessages.account}>
      {user ? <p>{authMessages.signedInAs(user.fullName)}</p> : null}
      <button type="button" onClick={signOut}>
        {authMessages.signOut}
      </button>
    </section>
  )
}

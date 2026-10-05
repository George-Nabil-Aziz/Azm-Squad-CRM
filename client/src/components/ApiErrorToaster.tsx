import { useEffect } from 'react'
import { Toaster, toast } from 'sonner'
import { onApiError } from '../api/client'
import { getApiErrorDescription, getApiErrorMessage } from '../api/error-messages'

/**
 * Mount once at the app root: renders the toast container and shows an error toast
 * for every failed API call. CRM-3 swaps sonner's <Toaster> for the shadcn wrapper
 * (client/src/components/ui/sonner.tsx); the toast() calls stay the same.
 */
export function ApiErrorToaster() {
  useEffect(
    () =>
      onApiError((error) => {
        // 401 is not a toast: the login form shows wrong credentials inline, and an expired
        // session clears the token so the app shows the sign-in form again (CRM-2).
        if (error.status === 401) return
        toast.error(getApiErrorMessage(error), { description: getApiErrorDescription(error) })
      }),
    [],
  )

  return <Toaster position="top-center" closeButton />
}

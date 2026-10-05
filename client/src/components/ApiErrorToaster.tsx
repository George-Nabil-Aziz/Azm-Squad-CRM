import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { Toaster } from '@/components/ui/sonner'
import { onApiError } from '../api/client'
import { getApiErrorDescription, getApiErrorMessage } from '../api/error-messages'

/**
 * Mount once at the app root: renders the toast container and shows an error toast
 * for every failed API call, in the current UI language and direction.
 */
export function ApiErrorToaster() {
  const { t, i18n } = useTranslation()

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

  return (
    <Toaster
      position="top-center"
      closeButton
      dir={i18n.dir()}
      containerAriaLabel={t('toast.notifications')}
      // Replaces the wrapper's toastOptions, so its "cn-toast" class is repeated here.
      toastOptions={{ classNames: { toast: 'cn-toast' }, closeButtonAriaLabel: t('toast.close') }}
    />
  )
}

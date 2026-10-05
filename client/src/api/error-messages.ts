import { i18n } from '../i18n/i18n'
import type { ApiError } from './errors'

// Toast text for a failed API call, in the current UI language (keys under "errors" in src/i18n/{en,ar}.json).
// The text is chosen by status, not by the server title, so every toast is translated on the client.
export function getApiErrorMessage(error: ApiError): string {
  switch (error.status) {
    case 0:
      return i18n.t('errors.network')
    case 400:
      return i18n.t('errors.badRequest')
    case 403:
      return i18n.t('errors.forbidden')
    case 404:
      return i18n.t('errors.notFound')
    case 409:
      return i18n.t('errors.conflict')
    default:
      return i18n.t('errors.generic')
  }
}

export function getApiErrorDescription(error: ApiError): string | undefined {
  return error.correlationId ? i18n.t('errors.reference', { id: error.correlationId }) : undefined
}

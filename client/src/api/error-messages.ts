import type { ApiError } from './errors'

// Temporary English text. CRM-4 moves these strings to client/src/i18n/{en,ar}.json
// and replaces this module with translation keys.
const messages = {
  network: 'Cannot reach the server. Check your connection and try again.',
  badRequest: 'The request is invalid. Check the entered data.',
  forbidden: 'You do not have permission to do this.',
  notFound: 'The requested item was not found.',
  conflict: 'This change conflicts with existing data.',
  generic: 'Something went wrong. Please try again.',
  reference: (id: string) => `Reference: ${id}`,
}

export function getApiErrorMessage(error: ApiError): string {
  switch (error.status) {
    case 0:
      return messages.network
    case 400:
      return messages.badRequest
    case 403:
      return messages.forbidden
    case 404:
      return messages.notFound
    case 409:
      return messages.conflict
    default:
      return messages.generic
  }
}

export function getApiErrorDescription(error: ApiError): string | undefined {
  return error.correlationId ? messages.reference(error.correlationId) : undefined
}

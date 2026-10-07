import type { TFunction } from 'i18next'
import { z } from 'zod'
import { ticketPriorities } from './ticket-values'

/** Client-side checks of the new-ticket dialog (the server validates again). Empty category = none. */
export function createTicketFormSchema(t: TFunction) {
  return z.object({
    customerId: z.string().min(1, t('tickets.customerRequired')),
    subject: z.string().trim().min(1, t('tickets.subjectRequired')).max(200),
    description: z.string().trim().max(10_000),
    categoryId: z.string(),
    departmentId: z.string(),
    priority: z.enum(ticketPriorities),
  })
}

export type TicketFormValues = z.infer<ReturnType<typeof createTicketFormSchema>>

/** Fields the API can report errors for (ProblemDetails `errors` keys). */
export const ticketFormFields = ['customerId', 'subject', 'description', 'categoryId', 'departmentId', 'priority'] as const

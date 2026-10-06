import type { TFunction } from 'i18next'
import { z } from 'zod'

/** Client-side checks of the portal "new request" form (the server validates again). */
export function createPortalTicketSchema(t: TFunction) {
  return z.object({
    subject: z.string().trim().min(1, t('portal.newTicket.subjectRequired')).max(200),
    description: z.string().trim().max(10_000),
    categoryId: z.string(),
  })
}

export type PortalTicketValues = z.infer<ReturnType<typeof createPortalTicketSchema>>

/** Fields the API can report errors for (ProblemDetails `errors` keys). */
export const portalTicketFields = ['subject', 'description', 'categoryId'] as const

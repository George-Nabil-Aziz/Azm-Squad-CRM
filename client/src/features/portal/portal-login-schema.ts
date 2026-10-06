import type { TFunction } from 'i18next'
import { z } from 'zod'

/** Client-side checks of the portal sign-in steps (the server validates again). */
export function createPortalEmailSchema(t: TFunction) {
  return z.object({
    email: z.string().trim().min(1, t('portal.login.emailRequired')).pipe(z.email(t('portal.login.emailInvalid'))),
  })
}

export function createPortalCodeSchema(t: TFunction) {
  return z.object({
    code: z.string().trim().regex(/^\d{6}$/, t('portal.login.codeInvalid')),
  })
}

export type PortalEmailValues = z.infer<ReturnType<typeof createPortalEmailSchema>>
export type PortalCodeValues = z.infer<ReturnType<typeof createPortalCodeSchema>>

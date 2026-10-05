import type { TFunction } from 'i18next'
import { z } from 'zod'

/**
 * Client-side checks before calling POST /api/auth/login (the server validates again).
 * Built with the current `t`, so the messages are in the current UI language.
 */
export function createLoginSchema(t: TFunction) {
  return z.object({
    email: z.string().trim().min(1, t('auth.emailRequired')).pipe(z.email(t('auth.emailInvalid'))),
    password: z.string().min(1, t('auth.passwordRequired')),
  })
}

export type LoginValues = z.infer<ReturnType<typeof createLoginSchema>>

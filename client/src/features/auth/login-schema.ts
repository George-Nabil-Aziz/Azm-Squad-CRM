import { z } from 'zod'
import { authMessages } from './auth-messages'

/** Client-side checks before calling POST /api/auth/login (the server validates again). */
export const loginSchema = z.object({
  email: z.string().trim().min(1, authMessages.emailRequired).pipe(z.email(authMessages.emailInvalid)),
  password: z.string().min(1, authMessages.passwordRequired),
})

export type LoginValues = z.infer<typeof loginSchema>

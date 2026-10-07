import type { TFunction } from 'i18next'
import { z } from 'zod'

/**
 * Quick client check before the API's real one (libphonenumber): optional leading +, digits in any script
 * (also ٠–٩), spaces, dots, dashes, brackets; at least 6 digits.
 */
export function isPhoneNumber(phone: string): boolean {
  return /^\+?[\p{Nd}\s().-]+$/u.test(phone) && (phone.match(/\p{Nd}/gu)?.length ?? 0) >= 6
}

/** Client-side checks of the customer dialog (the server validates again). Email and phone may stay empty. */
export function createCustomerFormSchema(t: TFunction) {
  return z.object({
    name: z.string().trim().min(1, t('customers.nameRequired')).max(200),
    email: z
      .string()
      .trim()
      .max(256)
      .refine((email) => email === '' || z.email().safeParse(email).success, t('customers.emailInvalid')),
    phone: z
      .string()
      .trim()
      .max(32)
      .refine((phone) => phone === '' || isPhoneNumber(phone), t('customers.phoneInvalid')),
    branchId: z.string(),
  })
}

export type CustomerFormValues = z.infer<ReturnType<typeof createCustomerFormSchema>>

/** Fields the API can report errors for (ProblemDetails `errors` keys). */
export const customerFormFields = ['name', 'email', 'phone', 'branchId'] as const

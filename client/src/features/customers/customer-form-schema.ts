import type { TFunction } from 'i18next'
import { z } from 'zod'

/** Same rule as the API (CustomerRequestValidator): optional leading +, digits, spaces, dashes, brackets; 6+ digits. */
export function isPhoneNumber(phone: string): boolean {
  return /^\+?[0-9 ()-]+$/.test(phone) && (phone.match(/[0-9]/g)?.length ?? 0) >= 6
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
  })
}

export type CustomerFormValues = z.infer<ReturnType<typeof createCustomerFormSchema>>

/** Fields the API can report errors for (ProblemDetails `errors` keys). */
export const customerFormFields = ['name', 'email', 'phone'] as const

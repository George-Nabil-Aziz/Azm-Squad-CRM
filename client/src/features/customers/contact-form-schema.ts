import type { TFunction } from 'i18next'
import { z } from 'zod'
import type { ContactType } from '@/api/customers'
import { isPhoneNumber } from './customer-form-schema'

/** Contact types in the order the form offers them. */
export const contactTypes = ['phone', 'email', 'whatsapp'] as const satisfies readonly ContactType[]

/** Client-side checks of the "add contact" form (the server validates again and normalizes numbers to E.164). */
export function createContactFormSchema(t: TFunction) {
  return z
    .object({
      type: z.enum(contactTypes),
      value: z.string().trim().min(1, t('customers.contacts.valueRequired')).max(256),
      isPrimary: z.boolean(),
    })
    .superRefine(({ type, value }, context) => {
      const valid = type === 'email' ? z.email().safeParse(value).success : isPhoneNumber(value)
      if (value !== '' && !valid) {
        context.addIssue({
          code: 'custom',
          path: ['value'],
          message: t(type === 'email' ? 'customers.emailInvalid' : 'customers.phoneInvalid'),
        })
      }
    })
}

export type ContactFormValues = z.infer<ReturnType<typeof createContactFormSchema>>

/** Fields the API can report errors for (ProblemDetails `errors` keys). */
export const contactFormFields = ['type', 'value'] as const

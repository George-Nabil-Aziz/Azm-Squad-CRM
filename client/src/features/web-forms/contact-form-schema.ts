import type { TFunction } from 'i18next'
import { z } from 'zod'

/** Client-side checks of the embedded contact form (the server validates again). */
export function createContactFormSchema(t: TFunction) {
  return z.object({
    name: z.string().trim().min(1, t('webForms.form.nameRequired')).max(200),
    email: z.string().trim().pipe(z.email(t('webForms.form.emailInvalid'))),
    subject: z.string().trim().min(1, t('webForms.form.subjectRequired')).max(200),
    message: z.string().trim().min(1, t('webForms.form.messageRequired')).max(10_000),
  })
}

export type ContactFormValues = z.infer<ReturnType<typeof createContactFormSchema>>

/** Fields the API can report errors for (ProblemDetails `errors` keys). */
export const contactFormFields = ['name', 'email', 'subject', 'message'] as const

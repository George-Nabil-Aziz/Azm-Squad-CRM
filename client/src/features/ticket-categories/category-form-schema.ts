import type { TFunction } from 'i18next'
import { z } from 'zod'

/** Client-side checks of the category dialog (the server validates again and checks uniqueness). */
export function createCategoryFormSchema(t: TFunction) {
  return z.object({
    name: z.string().trim().min(1, t('ticketCategories.nameRequired')).max(100),
    isActive: z.boolean(),
  })
}

export type CategoryFormValues = z.infer<ReturnType<typeof createCategoryFormSchema>>

/** Fields the API can report errors for (ProblemDetails `errors` keys). */
export const categoryFormFields = ['name'] as const

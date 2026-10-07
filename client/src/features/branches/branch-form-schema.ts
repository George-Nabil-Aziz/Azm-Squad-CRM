import type { TFunction } from 'i18next'
import { z } from 'zod'

/** Client-side checks of the branch dialog (the server validates again and checks uniqueness). */
export function createBranchFormSchema(t: TFunction) {
  return z.object({
    name: z.string().trim().min(1, t('branches.nameRequired')).max(100),
    isActive: z.boolean(),
  })
}

export type BranchFormValues = z.infer<ReturnType<typeof createBranchFormSchema>>

/** Fields the API can report errors for (ProblemDetails `errors` keys). */
export const branchFormFields = ['name'] as const

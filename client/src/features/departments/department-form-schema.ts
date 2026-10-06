import type { TFunction } from 'i18next'
import { z } from 'zod'

/** Client-side checks of the department dialog (the server validates again and checks uniqueness). */
export function createDepartmentFormSchema(t: TFunction) {
  return z.object({
    name: z.string().trim().min(1, t('departments.nameRequired')).max(100),
    isActive: z.boolean(),
  })
}

export type DepartmentFormValues = z.infer<ReturnType<typeof createDepartmentFormSchema>>

/** Fields the API can report errors for (ProblemDetails `errors` keys). */
export const departmentFormFields = ['name'] as const

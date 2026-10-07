import type { TFunction } from 'i18next'
import { z } from 'zod'
import { roleNames } from '@/api/users'

/** Same policy as the API (8+ characters, upper-case, lower-case, digit, symbol); the server checks it again. */
export function isStrongPassword(password: string): boolean {
  return (
    password.length >= 8 &&
    /[A-Z]/.test(password) &&
    /[a-z]/.test(password) &&
    /\d/.test(password) &&
    /[^A-Za-z0-9]/.test(password)
  )
}

/**
 * Client-side checks of the user dialog (the server validates again).
 * "create" requires a strong password; "edit" has no password field (the value is ignored).
 */
export function createUserFormSchema(t: TFunction, mode: 'create' | 'edit') {
  return z.object({
    fullName: z.string().trim().min(1, t('users.fullNameRequired')).max(200),
    email: z.string().trim().min(1, t('users.emailRequired')).pipe(z.email(t('users.emailInvalid'))),
    password: mode === 'create' ? z.string().refine(isStrongPassword, t('users.passwordWeak')) : z.string(),
    roles: z.array(z.enum(roleNames)).min(1, t('users.rolesRequired')),
    departmentIds: z.array(z.string()),
    branchId: z.string(),
  })
}

export type UserFormValues = z.infer<ReturnType<typeof createUserFormSchema>>

/** Fields the API can report errors for (ProblemDetails `errors` keys). */
export const userFormFields = ['fullName', 'email', 'password', 'roles', 'departmentIds', 'branchId'] as const

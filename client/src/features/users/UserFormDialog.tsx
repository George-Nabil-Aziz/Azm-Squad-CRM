import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { isApiError } from '@/api/errors'
import { createUser, roleNames, updateUser, type User } from '@/api/users'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Field, FieldError, FieldGroup, FieldLabel, FieldLegend, FieldSet } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { createUserFormSchema, userFormFields, type UserFormValues } from './user-form-schema'
import { usersQueryKey } from './useUsers'

interface UserFormDialogProps {
  /** The user to edit; without it the dialog creates a new user. */
  user?: User
  onClose: () => void
}

/** Create / edit dialog. Mounted only while open (key per user), so the form always starts from fresh values. */
export function UserFormDialog({ user, onClose }: UserFormDialogProps) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const mode = user ? 'edit' : 'create'
  const schema = useMemo(() => createUserFormSchema(t, mode), [t, mode])
  const form = useForm<UserFormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      fullName: user?.fullName ?? '',
      email: user?.email ?? '',
      password: '',
      roles: user?.roles ?? [],
    },
  })

  const save = useMutation({
    mutationFn: ({ password, ...values }: UserFormValues) =>
      user ? updateUser(user.id, values) : createUser({ ...values, password }),
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: usersQueryKey })
      toast.success(t(user ? 'users.updated' : 'users.created', { name: saved.fullName }))
      onClose()
    },
  })

  async function onSubmit(values: UserFormValues) {
    try {
      await save.mutateAsync(values)
    } catch (caught) {
      // 400: show the server's field messages (already in the UI language) next to the fields.
      // Every failure also shows a toast (ApiErrorToaster).
      if (!isApiError(caught) || caught.status !== 400) return
      for (const field of userFormFields) {
        const message = caught.problem?.errors?.[field]?.[0]
        if (message) form.setError(field, { message })
      }
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>{t(user ? 'users.editTitle' : 'users.createTitle')}</DialogTitle>
          <DialogDescription>{t(user ? 'users.editDescription' : 'users.createDescription')}</DialogDescription>
        </DialogHeader>
        <form noValidate onSubmit={form.handleSubmit(onSubmit)}>
          <FieldGroup>
            <Controller
              name="fullName"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="user-full-name">{t('users.fullName')}</FieldLabel>
                  <Input {...field} id="user-full-name" autoComplete="off" aria-invalid={fieldState.invalid} />
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </Field>
              )}
            />
            <Controller
              name="email"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="user-email">{t('users.email')}</FieldLabel>
                  <Input {...field} id="user-email" type="email" autoComplete="off" aria-invalid={fieldState.invalid} />
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </Field>
              )}
            />
            {user ? null : (
              <Controller
                name="password"
                control={form.control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="user-password">{t('users.password')}</FieldLabel>
                    <Input
                      {...field}
                      id="user-password"
                      type="password"
                      autoComplete="new-password"
                      aria-invalid={fieldState.invalid}
                    />
                    {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                  </Field>
                )}
              />
            )}
            <Controller
              name="roles"
              control={form.control}
              render={({ field, fieldState }) => (
                <FieldSet data-invalid={fieldState.invalid}>
                  <FieldLegend variant="label">{t('users.roles')}</FieldLegend>
                  {roleNames.map((role) => (
                    <Field key={role} orientation="horizontal">
                      <Checkbox
                        id={`user-role-${role}`}
                        checked={field.value.includes(role)}
                        aria-invalid={fieldState.invalid}
                        onCheckedChange={(checked) =>
                          field.onChange(
                            checked === true ? [...field.value, role] : field.value.filter((value) => value !== role),
                          )
                        }
                      />
                      <FieldLabel htmlFor={`user-role-${role}`}>{t(`users.roleNames.${role}`)}</FieldLabel>
                    </Field>
                  ))}
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </FieldSet>
              )}
            />
            <DialogFooter>
              <Button type="button" variant="outline" onClick={onClose}>
                {t('users.cancel')}
              </Button>
              <Button type="submit" disabled={form.formState.isSubmitting}>
                {form.formState.isSubmitting ? t('users.saving') : t('users.save')}
              </Button>
            </DialogFooter>
          </FieldGroup>
        </form>
      </DialogContent>
    </Dialog>
  )
}

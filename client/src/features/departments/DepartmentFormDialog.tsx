import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { createDepartment, updateDepartment, type Department } from '@/api/departments'
import { isApiError } from '@/api/errors'
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
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import {
  createDepartmentFormSchema,
  departmentFormFields,
  type DepartmentFormValues,
} from './department-form-schema'
import { departmentsQueryKey } from './useDepartments'

interface DepartmentFormDialogProps {
  /** The department to edit; without it the dialog creates a new (active) department. */
  department?: Department
  onClose: () => void
}

/** Create / edit dialog. Editing also (de)activates the department. Mounted only while open. */
export function DepartmentFormDialog({ department, onClose }: DepartmentFormDialogProps) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const schema = useMemo(() => createDepartmentFormSchema(t), [t])
  const form = useForm<DepartmentFormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: department?.name ?? '', isActive: department?.isActive ?? true },
  })

  const save = useMutation({
    mutationFn: (values: DepartmentFormValues) =>
      department ? updateDepartment(department.id, values) : createDepartment(values),
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: departmentsQueryKey })
      toast.success(t(department ? 'departments.updated' : 'departments.created', { name: saved.name }))
      onClose()
    },
  })

  async function onSubmit(values: DepartmentFormValues) {
    try {
      await save.mutateAsync(values)
    } catch (caught) {
      // 400 (missing or duplicate name): the server's message, already in the UI language, next to the field.
      if (!isApiError(caught) || caught.status !== 400) return
      for (const field of departmentFormFields) {
        const message = caught.problem?.errors?.[field]?.[0]
        if (message) form.setError(field, { message })
      }
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>{t(department ? 'departments.editTitle' : 'departments.createTitle')}</DialogTitle>
          <DialogDescription>
            {t(department ? 'departments.editDescription' : 'departments.createDescription')}
          </DialogDescription>
        </DialogHeader>
        <form noValidate onSubmit={form.handleSubmit(onSubmit)}>
          <FieldGroup>
            <Controller
              name="name"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="department-name">{t('departments.name')}</FieldLabel>
                  <Input {...field} id="department-name" autoComplete="off" aria-invalid={fieldState.invalid} />
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </Field>
              )}
            />
            {department ? (
              <Controller
                name="isActive"
                control={form.control}
                render={({ field }) => (
                  <Field orientation="horizontal">
                    <Checkbox
                      id="department-active"
                      checked={field.value}
                      onCheckedChange={(checked) => field.onChange(checked === true)}
                    />
                    <FieldLabel htmlFor="department-active">{t('departments.isActive')}</FieldLabel>
                  </Field>
                )}
              />
            ) : null}
            <DialogFooter>
              <Button type="button" variant="outline" onClick={onClose}>
                {t('departments.cancel')}
              </Button>
              <Button type="submit" disabled={form.formState.isSubmitting}>
                {form.formState.isSubmitting ? t('departments.saving') : t('departments.save')}
              </Button>
            </DialogFooter>
          </FieldGroup>
        </form>
      </DialogContent>
    </Dialog>
  )
}

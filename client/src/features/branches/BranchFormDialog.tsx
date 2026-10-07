import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { createBranch, updateBranch, type Branch } from '@/api/branches'
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
  createBranchFormSchema,
  branchFormFields,
  type BranchFormValues,
} from './branch-form-schema'
import { branchesQueryKey } from './useBranches'

interface BranchFormDialogProps {
  /** The branch to edit; without it the dialog creates a new (active) branch. */
  branch?: Branch
  onClose: () => void
}

/** Create / edit dialog. Editing also (de)activates the branch. Mounted only while open. */
export function BranchFormDialog({ branch, onClose }: BranchFormDialogProps) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const schema = useMemo(() => createBranchFormSchema(t), [t])
  const form = useForm<BranchFormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: branch?.name ?? '', isActive: branch?.isActive ?? true },
  })

  const save = useMutation({
    mutationFn: (values: BranchFormValues) =>
      branch ? updateBranch(branch.id, values) : createBranch(values),
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: branchesQueryKey })
      toast.success(t(branch ? 'branches.updated' : 'branches.created', { name: saved.name }))
      onClose()
    },
  })

  async function onSubmit(values: BranchFormValues) {
    try {
      await save.mutateAsync(values)
    } catch (caught) {
      // 400 (missing or duplicate name): the server's message, already in the UI language, next to the field.
      if (!isApiError(caught) || caught.status !== 400) return
      for (const field of branchFormFields) {
        const message = caught.problem?.errors?.[field]?.[0]
        if (message) form.setError(field, { message })
      }
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>{t(branch ? 'branches.editTitle' : 'branches.createTitle')}</DialogTitle>
          <DialogDescription>
            {t(branch ? 'branches.editDescription' : 'branches.createDescription')}
          </DialogDescription>
        </DialogHeader>
        <form noValidate onSubmit={form.handleSubmit(onSubmit)}>
          <FieldGroup>
            <Controller
              name="name"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="branch-name">{t('branches.name')}</FieldLabel>
                  <Input {...field} id="branch-name" autoComplete="off" aria-invalid={fieldState.invalid} />
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </Field>
              )}
            />
            {branch ? (
              <Controller
                name="isActive"
                control={form.control}
                render={({ field }) => (
                  <Field orientation="horizontal">
                    <Checkbox
                      id="branch-active"
                      checked={field.value}
                      onCheckedChange={(checked) => field.onChange(checked === true)}
                    />
                    <FieldLabel htmlFor="branch-active">{t('branches.isActive')}</FieldLabel>
                  </Field>
                )}
              />
            ) : null}
            <DialogFooter>
              <Button type="button" variant="outline" onClick={onClose}>
                {t('branches.cancel')}
              </Button>
              <Button type="submit" disabled={form.formState.isSubmitting}>
                {form.formState.isSubmitting ? t('branches.saving') : t('branches.save')}
              </Button>
            </DialogFooter>
          </FieldGroup>
        </form>
      </DialogContent>
    </Dialog>
  )
}

import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { isApiError } from '@/api/errors'
import { createTicketCategory, updateTicketCategory, type TicketCategory } from '@/api/ticket-categories'
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
import { categoryFormFields, createCategoryFormSchema, type CategoryFormValues } from './category-form-schema'
import { ticketCategoriesQueryKey } from './useTicketCategories'

interface TicketCategoryFormDialogProps {
  /** The category to edit; without it the dialog creates a new (active) category. */
  category?: TicketCategory
  onClose: () => void
}

/** Create / edit dialog. Editing also (de)activates the category. Mounted only while open. */
export function TicketCategoryFormDialog({ category, onClose }: TicketCategoryFormDialogProps) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const schema = useMemo(() => createCategoryFormSchema(t), [t])
  const form = useForm<CategoryFormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: category?.name ?? '', isActive: category?.isActive ?? true },
  })

  const save = useMutation({
    mutationFn: (values: CategoryFormValues) =>
      category ? updateTicketCategory(category.id, values) : createTicketCategory(values),
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: ticketCategoriesQueryKey })
      toast.success(t(category ? 'ticketCategories.updated' : 'ticketCategories.created', { name: saved.name }))
      onClose()
    },
  })

  async function onSubmit(values: CategoryFormValues) {
    try {
      await save.mutateAsync(values)
    } catch (caught) {
      // 400 (missing or duplicate name): the server's message, already in the UI language, next to the field.
      if (!isApiError(caught) || caught.status !== 400) return
      for (const field of categoryFormFields) {
        const message = caught.problem?.errors?.[field]?.[0]
        if (message) form.setError(field, { message })
      }
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>{t(category ? 'ticketCategories.editTitle' : 'ticketCategories.createTitle')}</DialogTitle>
          <DialogDescription>
            {t(category ? 'ticketCategories.editDescription' : 'ticketCategories.createDescription')}
          </DialogDescription>
        </DialogHeader>
        <form noValidate onSubmit={form.handleSubmit(onSubmit)}>
          <FieldGroup>
            <Controller
              name="name"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="category-name">{t('ticketCategories.name')}</FieldLabel>
                  <Input {...field} id="category-name" autoComplete="off" aria-invalid={fieldState.invalid} />
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </Field>
              )}
            />
            {category ? (
              <Controller
                name="isActive"
                control={form.control}
                render={({ field }) => (
                  <Field orientation="horizontal">
                    <Checkbox
                      id="category-active"
                      checked={field.value}
                      onCheckedChange={(checked) => field.onChange(checked === true)}
                    />
                    <FieldLabel htmlFor="category-active">{t('ticketCategories.isActive')}</FieldLabel>
                  </Field>
                )}
              />
            ) : null}
            <DialogFooter>
              <Button type="button" variant="outline" onClick={onClose}>
                {t('ticketCategories.cancel')}
              </Button>
              <Button type="submit" disabled={form.formState.isSubmitting}>
                {form.formState.isSubmitting ? t('ticketCategories.saving') : t('ticketCategories.save')}
              </Button>
            </DialogFooter>
          </FieldGroup>
        </form>
      </DialogContent>
    </Dialog>
  )
}

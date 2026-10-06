import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { isApiError } from '@/api/errors'
import { createKbCategory, updateKbCategory, type KbCategory } from '@/api/knowledge-base'
import { Button } from '@/components/ui/button'
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
import { createKbCategoryFormSchema, kbCategoryFormFields, type KbCategoryFormValues } from './kb-schemas'
import { kbQueryKey } from './useKnowledgeBase'

/** Create / edit dialog of a category: a name in English and/or Arabic. Mounted only while open. */
export function CategoryFormDialog({ category, onClose }: { category?: KbCategory; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const schema = useMemo(() => createKbCategoryFormSchema(t), [t])
  const form = useForm<KbCategoryFormValues>({
    resolver: zodResolver(schema),
    defaultValues: { nameEn: category?.nameEn ?? '', nameAr: category?.nameAr ?? '' },
  })

  const save = useMutation({
    mutationFn: (values: KbCategoryFormValues) => {
      const request = { nameEn: values.nameEn || null, nameAr: values.nameAr || null }
      return category ? updateKbCategory(category.id, request) : createKbCategory(request)
    },
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: kbQueryKey })
      toast.success(t(category ? 'knowledgeBase.categoryUpdated' : 'knowledgeBase.categoryCreated', { name: saved.name }))
      onClose()
    },
  })

  async function onSubmit(values: KbCategoryFormValues) {
    try {
      await save.mutateAsync(values)
    } catch (caught) {
      if (!isApiError(caught) || caught.status !== 400) return
      for (const field of kbCategoryFormFields) {
        const message = caught.problem?.errors?.[field]?.[0]
        if (message) form.setError(field, { message })
      }
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>{t(category ? 'knowledgeBase.editCategoryTitle' : 'knowledgeBase.createCategoryTitle')}</DialogTitle>
          <DialogDescription>{t('knowledgeBase.categoryDescription')}</DialogDescription>
        </DialogHeader>
        <form noValidate onSubmit={form.handleSubmit(onSubmit)}>
          <FieldGroup>
            <Controller
              name="nameEn"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="kb-category-name-en">{t('knowledgeBase.nameEn')}</FieldLabel>
                  <Input {...field} id="kb-category-name-en" dir="ltr" autoComplete="off" aria-invalid={fieldState.invalid} />
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </Field>
              )}
            />
            <Controller
              name="nameAr"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="kb-category-name-ar">{t('knowledgeBase.nameAr')}</FieldLabel>
                  <Input {...field} id="kb-category-name-ar" dir="rtl" autoComplete="off" aria-invalid={fieldState.invalid} />
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </Field>
              )}
            />
            <DialogFooter>
              <Button type="button" variant="outline" onClick={onClose}>
                {t('knowledgeBase.cancel')}
              </Button>
              <Button type="submit" disabled={form.formState.isSubmitting}>
                {form.formState.isSubmitting ? t('knowledgeBase.saving') : t('knowledgeBase.save')}
              </Button>
            </DialogFooter>
          </FieldGroup>
        </form>
      </DialogContent>
    </Dialog>
  )
}

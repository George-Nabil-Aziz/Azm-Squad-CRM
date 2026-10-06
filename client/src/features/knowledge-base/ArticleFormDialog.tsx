import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'
import { Controller, useForm, type Control, type FieldPath } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { isApiError } from '@/api/errors'
import { createKbArticle, updateKbArticle, type KbArticle } from '@/api/knowledge-base'
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
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Textarea } from '@/components/ui/textarea'
import { articleFormFields, createArticleFormSchema, type ArticleFormValues } from './kb-schemas'
import { kbQueryKey, useKbCategories } from './useKnowledgeBase'

interface ArticleFormDialogProps {
  /** The article to edit; without it the dialog creates a new article (saved as a draft). */
  article?: KbArticle
  onClose: () => void
}

type ArticleFieldName = FieldPath<ArticleFormValues>

interface TextFieldProps {
  control: Control<ArticleFormValues>
  name: ArticleFieldName
  label: string
  id: string
  multiline?: boolean
  dir?: 'rtl' | 'ltr'
}

function TextField({ control, name, label, id, multiline, dir }: TextFieldProps) {
  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState }) => (
        <Field data-invalid={fieldState.invalid}>
          <FieldLabel htmlFor={id}>{label}</FieldLabel>
          {multiline ? (
            <Textarea {...field} id={id} rows={6} dir={dir} aria-invalid={fieldState.invalid} />
          ) : (
            <Input {...field} id={id} dir={dir} autoComplete="off" aria-invalid={fieldState.invalid} />
          )}
          {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
        </Field>
      )}
    />
  )
}

/** Create / edit dialog: category, English version and Arabic version (title + body each). Mounted only while open. */
export function ArticleFormDialog({ article, onClose }: ArticleFormDialogProps) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const categories = useKbCategories()
  const schema = useMemo(() => createArticleFormSchema(t), [t])
  const form = useForm<ArticleFormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      categoryId: article?.categoryId ?? '',
      titleEn: article?.titleEn ?? '',
      bodyEn: article?.bodyEn ?? '',
      titleAr: article?.titleAr ?? '',
      bodyAr: article?.bodyAr ?? '',
    },
  })

  const save = useMutation({
    mutationFn: (values: ArticleFormValues) => {
      const request = {
        categoryId: values.categoryId,
        titleEn: values.titleEn || null,
        bodyEn: values.bodyEn || null,
        titleAr: values.titleAr || null,
        bodyAr: values.bodyAr || null,
      }
      return article ? updateKbArticle(article.id, request) : createKbArticle(request)
    },
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: kbQueryKey })
      toast.success(t(article ? 'knowledgeBase.articleUpdated' : 'knowledgeBase.articleCreated', { title: saved.title }))
      onClose()
    },
  })

  async function onSubmit(values: ArticleFormValues) {
    try {
      await save.mutateAsync(values)
    } catch (caught) {
      // 400: the server's field messages (already in the UI language) next to the fields.
      if (!isApiError(caught) || caught.status !== 400) return
      const errors = caught.problem?.errors
      for (const field of articleFormFields) {
        const message = errors?.[field]?.[0] ?? (field === 'titleEn' ? errors?.title?.[0] : undefined)
        if (message) form.setError(field, { message })
      }
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent showCloseButton={false} className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t(article ? 'knowledgeBase.editArticleTitle' : 'knowledgeBase.createArticleTitle')}</DialogTitle>
          <DialogDescription>{t('knowledgeBase.articleDescription')}</DialogDescription>
        </DialogHeader>
        <form noValidate onSubmit={form.handleSubmit(onSubmit)}>
          <FieldGroup>
            <Controller
              name="categoryId"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="kb-article-category">{t('knowledgeBase.category')}</FieldLabel>
                  <NativeSelect {...field} id="kb-article-category" className="w-full" aria-invalid={fieldState.invalid}>
                    <NativeSelectOption value="">{t('knowledgeBase.categoryPlaceholder')}</NativeSelectOption>
                    {categories.data?.map((category) => (
                      <NativeSelectOption key={category.id} value={category.id}>
                        {category.name}
                      </NativeSelectOption>
                    ))}
                  </NativeSelect>
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </Field>
              )}
            />
            <TextField control={form.control} name="titleEn" id="kb-title-en" label={t('knowledgeBase.titleEn')} dir="ltr" />
            <TextField control={form.control} name="bodyEn" id="kb-body-en" label={t('knowledgeBase.bodyEn')} multiline dir="ltr" />
            <TextField control={form.control} name="titleAr" id="kb-title-ar" label={t('knowledgeBase.titleAr')} dir="rtl" />
            <TextField control={form.control} name="bodyAr" id="kb-body-ar" label={t('knowledgeBase.bodyAr')} multiline dir="rtl" />
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

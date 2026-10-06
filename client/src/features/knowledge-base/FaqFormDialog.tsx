import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'
import { Controller, useForm, type Control, type FieldPath } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { isApiError } from '@/api/errors'
import { createKbFaq, updateKbFaq, type KbFaq } from '@/api/knowledge-base'
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
import { Textarea } from '@/components/ui/textarea'
import { createFaqFormSchema, faqFormFields, type FaqFormValues } from './kb-schemas'
import { kbQueryKey } from './useKnowledgeBase'

type FaqTextFieldName = Extract<FieldPath<FaqFormValues>, 'questionEn' | 'answerEn' | 'questionAr' | 'answerAr' | 'displayOrder'>

interface FaqTextFieldProps {
  control: Control<FaqFormValues>
  name: FaqTextFieldName
  label: string
  id: string
  multiline?: boolean
  dir?: 'rtl' | 'ltr'
  inputMode?: 'numeric'
}

function FaqTextField({ control, name, label, id, multiline, dir, inputMode }: FaqTextFieldProps) {
  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState }) => (
        <Field data-invalid={fieldState.invalid}>
          <FieldLabel htmlFor={id}>{label}</FieldLabel>
          {multiline ? (
            <Textarea {...field} id={id} rows={4} dir={dir} aria-invalid={fieldState.invalid} />
          ) : (
            <Input {...field} id={id} dir={dir} inputMode={inputMode} autoComplete="off" aria-invalid={fieldState.invalid} />
          )}
          {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
        </Field>
      )}
    />
  )
}

/** Create / edit dialog of a FAQ: English and Arabic question + answer, display order, published. Mounted only while open. */
export function FaqFormDialog({ faq, onClose }: { faq?: KbFaq; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const schema = useMemo(() => createFaqFormSchema(t), [t])
  const form = useForm<FaqFormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      questionEn: faq?.questionEn ?? '',
      answerEn: faq?.answerEn ?? '',
      questionAr: faq?.questionAr ?? '',
      answerAr: faq?.answerAr ?? '',
      displayOrder: faq ? String(faq.displayOrder) : '',
      isPublished: faq?.isPublished ?? false,
    },
  })

  const save = useMutation({
    mutationFn: (values: FaqFormValues) => {
      const request = {
        questionEn: values.questionEn || null,
        answerEn: values.answerEn || null,
        questionAr: values.questionAr || null,
        answerAr: values.answerAr || null,
        displayOrder: values.displayOrder === '' ? null : Number(values.displayOrder),
        isPublished: values.isPublished,
      }
      return faq ? updateKbFaq(faq.id, request) : createKbFaq(request)
    },
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: kbQueryKey })
      toast.success(t(faq ? 'knowledgeBase.faqUpdated' : 'knowledgeBase.faqCreated', { question: saved.question }))
      onClose()
    },
  })

  async function onSubmit(values: FaqFormValues) {
    try {
      await save.mutateAsync(values)
    } catch (caught) {
      if (!isApiError(caught) || caught.status !== 400) return
      const errors = caught.problem?.errors
      for (const field of faqFormFields) {
        const message = errors?.[field]?.[0] ?? (field === 'questionEn' ? errors?.question?.[0] : undefined)
        if (message) form.setError(field, { message })
      }
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent showCloseButton={false} className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t(faq ? 'knowledgeBase.editFaqTitle' : 'knowledgeBase.createFaqTitle')}</DialogTitle>
          <DialogDescription>{t('knowledgeBase.faqDescription')}</DialogDescription>
        </DialogHeader>
        <form noValidate onSubmit={form.handleSubmit(onSubmit)}>
          <FieldGroup>
            <FaqTextField control={form.control} name="questionEn" id="kb-faq-question-en" label={t('knowledgeBase.questionEn')} dir="ltr" />
            <FaqTextField control={form.control} name="answerEn" id="kb-faq-answer-en" label={t('knowledgeBase.answerEn')} multiline dir="ltr" />
            <FaqTextField control={form.control} name="questionAr" id="kb-faq-question-ar" label={t('knowledgeBase.questionAr')} dir="rtl" />
            <FaqTextField control={form.control} name="answerAr" id="kb-faq-answer-ar" label={t('knowledgeBase.answerAr')} multiline dir="rtl" />
            <FaqTextField
              control={form.control}
              name="displayOrder"
              id="kb-faq-order"
              label={t('knowledgeBase.displayOrder')}
              inputMode="numeric"
            />
            <Controller
              name="isPublished"
              control={form.control}
              render={({ field }) => (
                <Field orientation="horizontal">
                  <Checkbox
                    id="kb-faq-published"
                    checked={field.value}
                    onCheckedChange={(checked) => field.onChange(checked === true)}
                  />
                  <FieldLabel htmlFor="kb-faq-published">{t('knowledgeBase.faqPublished')}</FieldLabel>
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

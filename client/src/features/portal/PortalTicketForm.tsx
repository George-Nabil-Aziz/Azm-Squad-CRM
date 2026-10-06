import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useMemo, useState, type ChangeEvent } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { isApiError } from '@/api/errors'
import { listPortalCategories, submitPortalTicket, type PortalTicket } from '@/api/portal'
import { Button } from '@/components/ui/button'
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Textarea } from '@/components/ui/textarea'
import { createPortalTicketSchema, portalTicketFields, type PortalTicketValues } from './portal-ticket-schema'

/** Subject, description, category and attachments; on success `onSubmitted` gets the new ticket (number included). */
export function PortalTicketForm({ onSubmitted }: { onSubmitted: (ticket: PortalTicket) => void }) {
  const { t } = useTranslation()
  const schema = useMemo(() => createPortalTicketSchema(t), [t])
  const [files, setFiles] = useState<File[]>([])
  const [fileError, setFileError] = useState<string | null>(null)
  const categories = useQuery({ queryKey: ['portal', 'categories'], queryFn: ({ signal }) => listPortalCategories(signal) })
  const form = useForm<PortalTicketValues>({
    resolver: zodResolver(schema),
    defaultValues: { subject: '', description: '', categoryId: '' },
  })
  const submit = useMutation({
    mutationFn: (values: PortalTicketValues) => submitPortalTicket({ ...values, files }),
    onSuccess: onSubmitted,
  })

  async function onSubmit(values: PortalTicketValues) {
    setFileError(null)
    try {
      await submit.mutateAsync(values)
    } catch (caught) {
      // 400: the server's messages (already in the UI language) next to the fields; every failure also toasts.
      if (!isApiError(caught) || caught.status !== 400) return
      const errors = caught.problem?.errors
      for (const field of portalTicketFields) {
        const message = errors?.[field]?.[0]
        if (message) form.setError(field, { message })
      }
      if (errors?.files?.[0]) setFileError(errors.files.join(' '))
    }
  }

  function onFiles(event: ChangeEvent<HTMLInputElement>) {
    setFiles(Array.from(event.target.files ?? []))
  }

  return (
    <form aria-label={t('portal.newTicket.title')} noValidate onSubmit={form.handleSubmit(onSubmit)}>
      <FieldGroup>
        <Controller
          name="subject"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="portal-subject">{t('portal.newTicket.subject')}</FieldLabel>
              <Input {...field} id="portal-subject" autoComplete="off" aria-invalid={fieldState.invalid} />
              {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
            </Field>
          )}
        />
        <Controller
          name="description"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="portal-description">{t('portal.newTicket.description')}</FieldLabel>
              <Textarea {...field} id="portal-description" rows={6} dir="auto" aria-invalid={fieldState.invalid} />
              {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
            </Field>
          )}
        />
        <Controller
          name="categoryId"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="portal-category">{t('portal.newTicket.category')}</FieldLabel>
              <NativeSelect {...field} id="portal-category" className="w-full" aria-invalid={fieldState.invalid}>
                <NativeSelectOption value="">{t('portal.newTicket.noCategory')}</NativeSelectOption>
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
        <Field data-invalid={fileError !== null}>
          <FieldLabel htmlFor="portal-files">{t('portal.newTicket.files')}</FieldLabel>
          <Input id="portal-files" type="file" multiple onChange={onFiles} aria-invalid={fileError !== null} />
          <FieldDescription>{t('portal.newTicket.filesHint')}</FieldDescription>
          {fileError ? <FieldError errors={[{ message: fileError }]} /> : null}
        </Field>
        <Button type="submit" disabled={form.formState.isSubmitting}>
          {form.formState.isSubmitting ? t('portal.newTicket.submitting') : t('portal.newTicket.submit')}
        </Button>
      </FieldGroup>
    </form>
  )
}

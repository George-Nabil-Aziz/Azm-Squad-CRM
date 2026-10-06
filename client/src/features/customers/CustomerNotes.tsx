import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { z } from 'zod'
import { addCustomerNote } from '@/api/customers'
import { isApiError } from '@/api/errors'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Textarea } from '@/components/ui/textarea'
import { Can } from '@/features/auth/Can'
import { useCustomerNotes } from './useCustomerNotes'
import { customersQueryKey } from './useCustomers'

const PAGE_SIZE = 10
const NOTE_MAX_LENGTH = 4000

/** Notes about a customer: add form (customers.manage), newest first, with author and time. */
export function CustomerNotes({ customerId }: { customerId: string }) {
  const { t, i18n } = useTranslation()
  const [page, setPage] = useState(1)
  const notes = useCustomerNotes(customerId, page, PAGE_SIZE)
  const totalPages = notes.data ? Math.max(1, Math.ceil(notes.data.totalCount / notes.data.pageSize)) : 1
  const formatTime = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' })

  return (
    <section className="flex flex-col gap-4">
      <h2 className="text-lg font-semibold">{t('customers.notes.title')}</h2>
      <Can permission={permissions.customersManage}>
        <AddNoteForm customerId={customerId} onAdded={() => setPage(1)} />
      </Can>
      {notes.isPending ? (
        <p className="text-muted-foreground">{t('customers.notes.loading')}</p>
      ) : notes.data && notes.data.items.length > 0 ? (
        <ul aria-label={t('customers.notes.title')} className="flex flex-col gap-3">
          {notes.data.items.map((note) => (
            <li key={note.id} className="flex flex-col gap-1 rounded-lg border p-3">
              <p dir="auto" className="whitespace-pre-line wrap-break-word">
                {note.text}
              </p>
              <p className="text-sm text-muted-foreground">
                {note.authorName ?? t('customers.timeline.system')}
                {' · '}
                <time dateTime={note.createdAt}>{formatTime.format(new Date(note.createdAt))}</time>
              </p>
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-muted-foreground">{t('customers.notes.empty')}</p>
      )}
      {totalPages > 1 ? (
        <div className="flex items-center justify-end gap-2">
          <span className="text-sm text-muted-foreground">{t('customers.notes.pageInfo', { page, pages: totalPages })}</span>
          <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
            {t('customers.notes.newer')}
          </Button>
          <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
            {t('customers.notes.older')}
          </Button>
        </div>
      ) : null}
    </section>
  )
}

function AddNoteForm({ customerId, onAdded }: { customerId: string; onAdded: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const schema = useMemo(
    () =>
      z.object({
        text: z
          .string()
          .trim()
          .min(1, t('customers.notes.textRequired'))
          .max(NOTE_MAX_LENGTH, t('customers.notes.textTooLong')),
      }),
    [t],
  )
  const form = useForm<{ text: string }>({ resolver: zodResolver(schema), defaultValues: { text: '' } })

  const add = useMutation({
    mutationFn: (text: string) => addCustomerNote(customerId, text),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: customersQueryKey })
      toast.success(t('customers.notes.added'))
      form.reset()
      onAdded()
    },
  })

  async function onSubmit({ text }: { text: string }) {
    try {
      await add.mutateAsync(text)
    } catch (caught) {
      const message = isApiError(caught) ? caught.problem?.errors?.text?.[0] : undefined
      if (message) form.setError('text', { message })
    }
  }

  return (
    <form noValidate onSubmit={form.handleSubmit(onSubmit)}>
      <FieldGroup>
        <Controller
          name="text"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="customer-note">{t('customers.notes.newNote')}</FieldLabel>
              <Textarea {...field} id="customer-note" dir="auto" rows={3} aria-invalid={fieldState.invalid} />
              {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
            </Field>
          )}
        />
        <div>
          <Button type="submit" disabled={form.formState.isSubmitting}>
            {form.formState.isSubmitting ? t('customers.notes.adding') : t('customers.notes.add')}
          </Button>
        </div>
      </FieldGroup>
    </form>
  )
}

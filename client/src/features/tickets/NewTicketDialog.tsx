import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { SearchIcon } from 'lucide-react'
import { useMemo, useState, type KeyboardEvent } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { isApiError } from '@/api/errors'
import { createTicket } from '@/api/tickets'
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
import { customersQueryKey, useCustomers } from '@/features/customers/useCustomers'
import { useDepartments } from '@/features/departments/useDepartments'
import { useTicketCategories } from '@/features/ticket-categories/useTicketCategories'
import { createTicketFormSchema, ticketFormFields, type TicketFormValues } from './ticket-form-schema'
import { ticketPriorities } from './ticket-values'
import { ticketsQueryKey } from './useTickets'

const CUSTOMER_PAGE_SIZE = 20

/** New-ticket dialog: customer (search + select), subject, description, active category, priority (default mid). */
export function NewTicketDialog({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const schema = useMemo(() => createTicketFormSchema(t), [t])
  const [searchText, setSearchText] = useState('')
  const [search, setSearch] = useState('')
  const customers = useCustomers({ search: search || undefined, page: 1, pageSize: CUSTOMER_PAGE_SIZE })
  const categories = useTicketCategories({ activeOnly: true })
  const departments = useDepartments({ activeOnly: true })
  const form = useForm<TicketFormValues>({
    resolver: zodResolver(schema),
    defaultValues: { customerId: '', subject: '', description: '', categoryId: '', departmentId: '', priority: 'mid' },
  })

  const create = useMutation({
    mutationFn: (values: TicketFormValues) =>
      createTicket({
        customerId: values.customerId,
        subject: values.subject,
        description: values.description || null,
        categoryId: values.categoryId || null,
        priority: values.priority,
        ...(values.departmentId ? { departmentId: values.departmentId } : {}),
      }),
    onSuccess: async (ticket) => {
      // The customer's timeline now shows the ticket too.
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ticketsQueryKey }),
        queryClient.invalidateQueries({ queryKey: customersQueryKey }),
      ])
      toast.success(t('tickets.created', { number: ticket.number }))
      onClose()
    },
  })

  async function onSubmit(values: TicketFormValues) {
    try {
      await create.mutateAsync(values)
    } catch (caught) {
      // 400: the server's field messages (already in the UI language) next to the fields; every failure also toasts.
      if (!isApiError(caught) || caught.status !== 400) return
      for (const field of ticketFormFields) {
        const message = caught.problem?.errors?.[field]?.[0]
        if (message) form.setError(field, { message })
      }
    }
  }

  function findCustomers() {
    setSearch(searchText.trim())
  }

  function onSearchKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter') {
      event.preventDefault()
      findCustomers()
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent showCloseButton={false} className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>{t('tickets.createTitle')}</DialogTitle>
          <DialogDescription>{t('tickets.createDescription')}</DialogDescription>
        </DialogHeader>
        <form noValidate onSubmit={form.handleSubmit(onSubmit)}>
          <FieldGroup>
            <div className="flex gap-2">
              <Input
                type="search"
                aria-label={t('tickets.customerSearch')}
                placeholder={t('tickets.customerSearch')}
                value={searchText}
                onChange={(event) => setSearchText(event.target.value)}
                onKeyDown={onSearchKeyDown}
              />
              <Button type="button" variant="outline" onClick={findCustomers}>
                <SearchIcon aria-hidden="true" />
                {t('tickets.customerFind')}
              </Button>
            </div>
            <Controller
              name="customerId"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="ticket-customer">{t('tickets.customer')}</FieldLabel>
                  <NativeSelect {...field} id="ticket-customer" className="w-full" aria-invalid={fieldState.invalid}>
                    <NativeSelectOption value="">{t('tickets.customerPlaceholder')}</NativeSelectOption>
                    {customers.data?.items.map((customer) => (
                      <NativeSelectOption key={customer.id} value={customer.id}>
                        {customer.name}
                      </NativeSelectOption>
                    ))}
                  </NativeSelect>
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </Field>
              )}
            />
            <Controller
              name="subject"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="ticket-subject">{t('tickets.subject')}</FieldLabel>
                  <Input {...field} id="ticket-subject" autoComplete="off" aria-invalid={fieldState.invalid} />
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </Field>
              )}
            />
            <Controller
              name="description"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="ticket-description">{t('tickets.descriptionField')}</FieldLabel>
                  <Textarea {...field} id="ticket-description" rows={4} aria-invalid={fieldState.invalid} />
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </Field>
              )}
            />
            {departments.data && departments.data.length > 0 ? (
              <Controller
                name="departmentId"
                control={form.control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="ticket-new-department">{t('tickets.department.label')}</FieldLabel>
                    <NativeSelect {...field} id="ticket-new-department" className="w-full" aria-invalid={fieldState.invalid}>
                      <NativeSelectOption value="">{t('tickets.department.none')}</NativeSelectOption>
                      {departments.data?.map((department) => (
                        <NativeSelectOption key={department.id} value={department.id}>
                          {department.name}
                        </NativeSelectOption>
                      ))}
                    </NativeSelect>
                    {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                  </Field>
                )}
              />
            ) : null}
            <div className="grid gap-4 sm:grid-cols-2">
              <Controller
                name="categoryId"
                control={form.control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="ticket-category">{t('tickets.category')}</FieldLabel>
                    <NativeSelect {...field} id="ticket-category" className="w-full" aria-invalid={fieldState.invalid}>
                      <NativeSelectOption value="">{t('tickets.noCategory')}</NativeSelectOption>
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
              <Controller
                name="priority"
                control={form.control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="ticket-priority">{t('tickets.priority')}</FieldLabel>
                    <NativeSelect {...field} id="ticket-priority" className="w-full" aria-invalid={fieldState.invalid}>
                      {ticketPriorities.map((priority) => (
                        <NativeSelectOption key={priority} value={priority}>
                          {t(`tickets.priorities.${priority}`)}
                        </NativeSelectOption>
                      ))}
                    </NativeSelect>
                    {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                  </Field>
                )}
              />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={onClose}>
                {t('tickets.cancel')}
              </Button>
              <Button type="submit" disabled={form.formState.isSubmitting}>
                {form.formState.isSubmitting ? t('tickets.creating') : t('tickets.create')}
              </Button>
            </DialogFooter>
          </FieldGroup>
        </form>
      </DialogContent>
    </Dialog>
  )
}

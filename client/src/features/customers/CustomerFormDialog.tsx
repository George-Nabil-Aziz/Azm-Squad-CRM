import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { createCustomer, updateCustomer, type Customer, type CustomerRequest } from '@/api/customers'
import { isApiError } from '@/api/errors'
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
import { useBranches } from '@/features/branches/useBranches'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { createCustomerFormSchema, customerFormFields, type CustomerFormValues } from './customer-form-schema'
import { customersQueryKey } from './useCustomers'

interface CustomerFormDialogProps {
  /** The customer to edit; without it the dialog creates a new customer. */
  customer?: Customer
  onClose: () => void
}

/** Empty email / phone are sent as null (the API stores "no value", not an empty string). */
function toRequest({ name, email, phone, branchId }: CustomerFormValues): CustomerRequest {
  return { name, email: email || null, phone: phone || null, ...(branchId ? { branchId } : {}) }
}

/** Create / edit dialog. Mounted only while open (key per customer), so the form always starts from fresh values. */
export function CustomerFormDialog({ customer, onClose }: CustomerFormDialogProps) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const schema = useMemo(() => createCustomerFormSchema(t), [t])
  const branches = useBranches({ activeOnly: true })
  const form = useForm<CustomerFormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: customer?.name ?? '',
      email: customer?.email ?? '',
      phone: customer?.phone ?? '',
      branchId: customer?.branchId ?? '',
    },
  })

  const save = useMutation({
    mutationFn: (values: CustomerFormValues) =>
      customer ? updateCustomer(customer.id, toRequest(values)) : createCustomer(toRequest(values)),
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: customersQueryKey })
      toast.success(t(customer ? 'customers.updated' : 'customers.created', { name: saved.name }))
      onClose()
    },
  })

  async function onSubmit(values: CustomerFormValues) {
    try {
      await save.mutateAsync(values)
    } catch (caught) {
      // 400: show the server's field messages (already in the UI language) next to the fields.
      // Every failure also shows a toast (ApiErrorToaster).
      if (!isApiError(caught) || caught.status !== 400) return
      for (const field of customerFormFields) {
        const message = caught.problem?.errors?.[field]?.[0]
        if (message) form.setError(field, { message })
      }
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>{t(customer ? 'customers.editTitle' : 'customers.createTitle')}</DialogTitle>
          <DialogDescription>{t(customer ? 'customers.editDescription' : 'customers.createDescription')}</DialogDescription>
        </DialogHeader>
        <form noValidate onSubmit={form.handleSubmit(onSubmit)}>
          <FieldGroup>
            <Controller
              name="name"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="customer-name">{t('customers.name')}</FieldLabel>
                  <Input {...field} id="customer-name" autoComplete="off" aria-invalid={fieldState.invalid} />
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </Field>
              )}
            />
            <Controller
              name="phone"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="customer-phone">{t('customers.phone')}</FieldLabel>
                  <Input
                    {...field}
                    id="customer-phone"
                    type="tel"
                    dir="ltr"
                    autoComplete="off"
                    aria-invalid={fieldState.invalid}
                  />
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </Field>
              )}
            />
            <Controller
              name="email"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="customer-email">{t('customers.email')}</FieldLabel>
                  <Input
                    {...field}
                    id="customer-email"
                    type="email"
                    dir="ltr"
                    autoComplete="off"
                    aria-invalid={fieldState.invalid}
                  />
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </Field>
              )}
            />
            {branches.data && branches.data.length > 0 ? (
              <Controller
                name="branchId"
                control={form.control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor="customer-branch">{t('customers.branch')}</FieldLabel>
                    <NativeSelect {...field} id="customer-branch" className="w-full" aria-invalid={fieldState.invalid}>
                      <NativeSelectOption value="">{t('customers.noBranch')}</NativeSelectOption>
                      {branches.data?.map((branch) => (
                        <NativeSelectOption key={branch.id} value={branch.id}>
                          {branch.name}
                        </NativeSelectOption>
                      ))}
                    </NativeSelect>
                    {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                  </Field>
                )}
              />
            ) : null}
            <DialogFooter>
              <Button type="button" variant="outline" onClick={onClose}>
                {t('customers.cancel')}
              </Button>
              <Button type="submit" disabled={form.formState.isSubmitting}>
                {form.formState.isSubmitting ? t('customers.saving') : t('customers.save')}
              </Button>
            </DialogFooter>
          </FieldGroup>
        </form>
      </DialogContent>
    </Dialog>
  )
}

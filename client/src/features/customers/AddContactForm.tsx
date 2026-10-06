import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { addCustomerContact } from '@/api/customers'
import { isApiError } from '@/api/errors'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { contactFormFields, contactTypes, createContactFormSchema, type ContactFormValues } from './contact-form-schema'
import { customersQueryKey } from './useCustomers'

/** Add-contact form of the contacts dialog: type, value (phone in any format or email), primary checkbox. */
export function AddContactForm({ customerId }: { customerId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const schema = useMemo(() => createContactFormSchema(t), [t])
  const form = useForm<ContactFormValues>({
    resolver: zodResolver(schema),
    defaultValues: { type: 'phone', value: '', isPrimary: false },
  })

  const add = useMutation({
    mutationFn: (values: ContactFormValues) => addCustomerContact(customerId, values),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: customersQueryKey })
      toast.success(t('customers.contacts.added'))
      form.reset()
    },
  })

  async function onSubmit(values: ContactFormValues) {
    try {
      await add.mutateAsync(values)
    } catch (caught) {
      // 400: the server's field messages; 409: the customer already has this contact. Both are already in the UI
      // language and are shown next to the fields (every failure also shows a toast).
      if (!isApiError(caught)) return
      if (caught.status === 400) {
        for (const field of contactFormFields) {
          const message = caught.problem?.errors?.[field]?.[0]
          if (message) form.setError(field, { message })
        }
      } else if (caught.status === 409 && caught.problem?.detail) {
        form.setError('value', { message: caught.problem.detail })
      }
    }
  }

  return (
    <form noValidate aria-labelledby="add-contact-title" onSubmit={form.handleSubmit(onSubmit)}>
      <FieldGroup>
        <h3 id="add-contact-title" className="font-medium">
          {t('customers.contacts.addTitle')}
        </h3>
        <div className="grid gap-4 sm:grid-cols-[10rem_1fr]">
          <Controller
            name="type"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="contact-type">{t('customers.contacts.type')}</FieldLabel>
                <NativeSelect {...field} id="contact-type" className="w-full" aria-invalid={fieldState.invalid}>
                  {contactTypes.map((type) => (
                    <NativeSelectOption key={type} value={type}>
                      {t(`customers.contacts.types.${type}`)}
                    </NativeSelectOption>
                  ))}
                </NativeSelect>
                {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
              </Field>
            )}
          />
          <Controller
            name="value"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="contact-value">{t('customers.contacts.value')}</FieldLabel>
                <Input {...field} id="contact-value" dir="ltr" autoComplete="off" aria-invalid={fieldState.invalid} />
                {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
              </Field>
            )}
          />
        </div>
        <Controller
          name="isPrimary"
          control={form.control}
          render={({ field }) => (
            <Field orientation="horizontal">
              <Checkbox
                id="contact-primary"
                checked={field.value}
                onCheckedChange={(checked) => field.onChange(checked === true)}
              />
              <FieldLabel htmlFor="contact-primary">{t('customers.contacts.isPrimary')}</FieldLabel>
            </Field>
          )}
        />
        <div>
          <Button type="submit" disabled={form.formState.isSubmitting}>
            {form.formState.isSubmitting ? t('customers.contacts.adding') : t('customers.contacts.add')}
          </Button>
        </div>
      </FieldGroup>
    </form>
  )
}

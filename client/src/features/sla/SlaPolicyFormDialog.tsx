import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { isApiError } from '@/api/errors'
import { updateSlaPolicy, type SlaPolicy } from '@/api/sla-policies'
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
import {
  createSlaPolicyFormSchema,
  slaPolicyFormFields,
  type SlaPolicyFormInput,
  type SlaPolicyFormValues,
} from './sla-policy-form-schema'
import { slaPoliciesQueryKey } from './useSlaPolicies'

interface SlaPolicyFormDialogProps {
  policy: SlaPolicy
  onClose: () => void
}

/** Edits the response and resolution minutes of one priority. Mounted only while open. */
export function SlaPolicyFormDialog({ policy, onClose }: SlaPolicyFormDialogProps) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const schema = useMemo(() => createSlaPolicyFormSchema(t), [t])
  const form = useForm<SlaPolicyFormInput, unknown, SlaPolicyFormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      responseMinutes: String(policy.responseMinutes),
      resolutionMinutes: String(policy.resolutionMinutes),
    },
  })
  const priority = t(`tickets.priorities.${policy.priority}`)

  const save = useMutation({
    mutationFn: (values: SlaPolicyFormValues) => updateSlaPolicy(policy.priority, values),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: slaPoliciesQueryKey })
      toast.success(t('sla.updated', { priority }))
      onClose()
    },
  })

  async function onSubmit(values: SlaPolicyFormValues) {
    try {
      await save.mutateAsync(values)
    } catch (caught) {
      // 400: the server's messages, already in the UI language, next to the fields.
      if (!isApiError(caught) || caught.status !== 400) return
      for (const field of slaPolicyFormFields) {
        const message = caught.problem?.errors?.[field]?.[0]
        if (message) form.setError(field, { message })
      }
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>{t('sla.editTitle', { priority })}</DialogTitle>
          <DialogDescription>{t('sla.editDescription')}</DialogDescription>
        </DialogHeader>
        <form noValidate onSubmit={form.handleSubmit(onSubmit)}>
          <FieldGroup>
            {slaPolicyFormFields.map((name) => (
              <Controller
                key={name}
                name={name}
                control={form.control}
                render={({ field, fieldState }) => (
                  <Field data-invalid={fieldState.invalid}>
                    <FieldLabel htmlFor={`sla-${name}`}>{t(`sla.${name}`)}</FieldLabel>
                    <Input
                      {...field}
                      id={`sla-${name}`}
                      type="number"
                      inputMode="numeric"
                      min={1}
                      step={1}
                      aria-invalid={fieldState.invalid}
                    />
                    {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                  </Field>
                )}
              />
            ))}
            <DialogFooter>
              <Button type="button" variant="outline" onClick={onClose}>
                {t('sla.cancel')}
              </Button>
              <Button type="submit" disabled={form.formState.isSubmitting}>
                {form.formState.isSubmitting ? t('sla.saving') : t('sla.save')}
              </Button>
            </DialogFooter>
          </FieldGroup>
        </form>
      </DialogContent>
    </Dialog>
  )
}

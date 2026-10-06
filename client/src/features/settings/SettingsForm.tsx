import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'
import { Controller, useForm, type Control, type FieldPath, type UseFormReturn } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { isApiError } from '@/api/errors'
import { securityModes, updateSettings, weekDays, type SecretName, type SystemSettings } from '@/api/settings'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import {
  createSettingsFormSchema,
  serverErrorFields,
  toFormValues,
  toRequest,
  type SettingsFormInput,
  type SettingsFormValues,
} from './settings-form-schema'
import { settingsQueryKey } from './useSettings'

type SettingsControl = Control<SettingsFormInput, unknown, SettingsFormValues>
type SettingsFormApi = UseFormReturn<SettingsFormInput, unknown, SettingsFormValues>
type TextFieldName = Exclude<FieldPath<SettingsFormInput>, 'days' | 'enabled' | 'clearSecrets'>

interface TextFieldProps {
  control: SettingsControl
  name: TextFieldName
  label: string
  type?: string
  hint?: string
  autoComplete?: string
}

function TextField({ control, name, label, type = 'text', hint, autoComplete }: TextFieldProps) {
  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState }) => (
        <Field data-invalid={fieldState.invalid}>
          <FieldLabel htmlFor={`settings-${name}`}>{label}</FieldLabel>
          <Input
            {...field}
            value={field.value as string}
            id={`settings-${name}`}
            type={type}
            autoComplete={autoComplete}
            aria-invalid={fieldState.invalid}
          />
          {hint ? <FieldDescription>{hint}</FieldDescription> : null}
          {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
        </Field>
      )}
    />
  )
}

interface SettingsFormProps {
  settings: SystemSettings
}

/** Business hours, general, email and WhatsApp settings of the system (SuperAdmin). Secrets are write-only. */
export function SettingsForm({ settings }: SettingsFormProps) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const schema = useMemo(() => createSettingsFormSchema(t), [t])
  const form = useForm<SettingsFormInput, unknown, SettingsFormValues>({
    resolver: zodResolver(schema),
    defaultValues: toFormValues(settings),
  })
  const control = form.control

  const save = useMutation({
    mutationFn: (values: SettingsFormValues) => updateSettings(toRequest(values)),
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: settingsQueryKey })
      form.reset(toFormValues(saved)) // secret inputs go back to empty
      toast.success(t('settings.saved'))
    },
  })

  async function onSubmit(values: SettingsFormValues) {
    try {
      await save.mutateAsync(values)
    } catch (caught) {
      // 400: the server's messages, already in the UI language, next to the fields.
      if (!isApiError(caught) || caught.status !== 400) return
      for (const [key, field] of Object.entries(serverErrorFields)) {
        const message = caught.problem?.errors?.[key]?.[0]
        if (message) form.setError(field, { message })
      }
    }
  }

  return (
    <form noValidate className="flex flex-col gap-6" onSubmit={form.handleSubmit(onSubmit)}>
      <Card>
        <CardHeader>
          <CardTitle>{t('settings.businessHours.title')}</CardTitle>
          <CardDescription>{t('settings.businessHours.description')}</CardDescription>
        </CardHeader>
        <CardContent>
          <FieldGroup>
            <Controller
              name="enabled"
              control={control}
              render={({ field }) => (
                <Field orientation="horizontal">
                  <Checkbox
                    id="settings-enabled"
                    checked={field.value}
                    onCheckedChange={(checked) => field.onChange(checked === true)}
                  />
                  <FieldLabel htmlFor="settings-enabled">{t('settings.businessHours.enabled')}</FieldLabel>
                </Field>
              )}
            />
            <Controller
              name="days"
              control={control}
              render={({ field, fieldState }) => (
                <fieldset className="flex flex-col gap-2" data-invalid={fieldState.invalid}>
                  <legend className="text-sm font-medium">{t('settings.businessHours.days')}</legend>
                  <div className="flex flex-wrap gap-4">
                    {weekDays.map((day) => (
                      <Field key={day} orientation="horizontal" className="w-auto">
                        <Checkbox
                          id={`settings-day-${day}`}
                          checked={field.value.includes(day)}
                          onCheckedChange={(checked) =>
                            field.onChange(
                              checked === true ? [...field.value, day] : field.value.filter((value) => value !== day),
                            )
                          }
                        />
                        <FieldLabel htmlFor={`settings-day-${day}`}>{t(`settings.weekDays.${day}`)}</FieldLabel>
                      </Field>
                    ))}
                  </div>
                  {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
                </fieldset>
              )}
            />
            <div className="grid gap-4 sm:grid-cols-2">
              <TextField control={control} name="start" label={t('settings.businessHours.start')} type="time" />
              <TextField control={control} name="end" label={t('settings.businessHours.end')} type="time" />
            </div>
          </FieldGroup>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t('settings.general.title')}</CardTitle>
        </CardHeader>
        <CardContent>
          <FieldGroup className="grid gap-4 sm:grid-cols-2">
            <TextField
              control={control}
              name="timeZone"
              label={t('settings.general.timeZone')}
              hint={t('settings.general.timeZoneHint')}
            />
            <TextField
              control={control}
              name="ticketPrefix"
              label={t('settings.general.ticketPrefix')}
              hint={t('settings.general.ticketPrefixHint')}
            />
          </FieldGroup>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t('settings.email.title')}</CardTitle>
          <CardDescription>{t('settings.email.description')}</CardDescription>
        </CardHeader>
        <CardContent>
          <FieldGroup className="grid gap-4 sm:grid-cols-2">
            <TextField control={control} name="fromAddress" label={t('settings.email.fromAddress')} type="email" />
            <TextField control={control} name="fromName" label={t('settings.email.fromName')} />
            <TextField control={control} name="smtpHost" label={t('settings.email.smtpHost')} />
            <TextField control={control} name="smtpPort" label={t('settings.email.smtpPort')} type="number" />
            <SecurityField control={control} name="smtpSecurity" label={t('settings.email.smtpSecurity')} />
            <TextField control={control} name="smtpUserName" label={t('settings.email.smtpUserName')} autoComplete="off" />
            <SecretField control={control} name="smtpPassword" settings={settings} form={form} />
            <TextField control={control} name="imapHost" label={t('settings.email.imapHost')} />
            <TextField control={control} name="imapPort" label={t('settings.email.imapPort')} type="number" />
            <SecurityField control={control} name="imapSecurity" label={t('settings.email.imapSecurity')} />
            <TextField control={control} name="imapUserName" label={t('settings.email.imapUserName')} autoComplete="off" />
            <SecretField control={control} name="imapPassword" settings={settings} form={form} />
            <TextField control={control} name="imapFolder" label={t('settings.email.imapFolder')} />
          </FieldGroup>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t('settings.whatsApp.title')}</CardTitle>
          <CardDescription>{t('settings.whatsApp.description')}</CardDescription>
        </CardHeader>
        <CardContent>
          <FieldGroup className="grid gap-4 sm:grid-cols-2">
            <TextField control={control} name="phoneNumberId" label={t('settings.whatsApp.phoneNumberId')} />
            <SecretField control={control} name="whatsAppAccessToken" settings={settings} form={form} />
            <SecretField control={control} name="whatsAppAppSecret" settings={settings} form={form} />
            <SecretField control={control} name="whatsAppVerifyToken" settings={settings} form={form} />
          </FieldGroup>
        </CardContent>
      </Card>

      <div className="flex justify-end">
        <Button type="submit" disabled={form.formState.isSubmitting}>
          {form.formState.isSubmitting ? t('settings.saving') : t('settings.save')}
        </Button>
      </div>
    </form>
  )
}

function SecurityField({
  control,
  name,
  label,
}: {
  control: SettingsControl
  name: 'smtpSecurity' | 'imapSecurity'
  label: string
}) {
  const { t } = useTranslation()
  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState }) => (
        <Field data-invalid={fieldState.invalid}>
          <FieldLabel htmlFor={`settings-${name}`}>{label}</FieldLabel>
          <NativeSelect id={`settings-${name}`} className="w-full" {...field}>
            {securityModes.map((mode) => (
              <NativeSelectOption key={mode} value={mode}>
                {t(`settings.securityModes.${mode}`)}
              </NativeSelectOption>
            ))}
          </NativeSelect>
          {fieldState.invalid ? <FieldError errors={[fieldState.error]} /> : null}
        </Field>
      )}
    />
  )
}

interface SecretFieldProps {
  control: SettingsControl
  name: SecretName
  settings: SystemSettings
  form: SettingsFormApi
}

/** A write-only secret: the saved value is never shown; empty keeps it, typing replaces it, the checkbox removes it. */
function SecretField({ control, name, settings, form }: SecretFieldProps) {
  const { t } = useTranslation()
  const isSet = settings.secretsSet[name]
  const clearing = form.watch('clearSecrets').includes(name)
  const label = t(`settings.secrets.${name}`)

  return (
    <div className="flex flex-col gap-2">
      <Controller
        name={name}
        control={control}
        render={({ field }) => (
          <Field>
            <FieldLabel htmlFor={`settings-${name}`}>{label}</FieldLabel>
            <Input
              {...field}
              id={`settings-${name}`}
              type="password"
              autoComplete="new-password"
              disabled={clearing}
              placeholder={isSet ? t('settings.secrets.keepPlaceholder') : undefined}
            />
            <FieldDescription>{isSet ? t('settings.secrets.isSet') : t('settings.secrets.notSet')}</FieldDescription>
          </Field>
        )}
      />
      {isSet ? (
        <Field orientation="horizontal">
          <Checkbox
            id={`settings-clear-${name}`}
            checked={clearing}
            onCheckedChange={(checked) => {
              const others = form.watch('clearSecrets').filter((value) => value !== name)
              form.setValue('clearSecrets', checked === true ? [...others, name] : others)
            }}
          />
          <FieldLabel htmlFor={`settings-clear-${name}`}>{t('settings.secrets.remove', { name: label })}</FieldLabel>
        </Field>
      ) : null}
    </div>
  )
}


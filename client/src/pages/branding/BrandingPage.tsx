import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import {
  MAX_LOGO_BYTES,
  removeBrandingLogo,
  updateBranding,
  uploadBrandingLogo,
  type Branding,
} from '@/api/branding'
import { isApiError } from '@/api/errors'
import { Button } from '@/components/ui/button'
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { BrandLogo } from '@/features/branding/BrandLogo'
import { parseHexColor } from '@/features/branding/branding-colors'
import { useBranding } from '@/features/branding/branding-context'

const brandingQueryKey = ['branding'] as const

/** Branding admin page (SuperAdmin): primary and secondary colour, logo upload / removal. Applied to the whole app at once. */
export function BrandingPage() {
  const branding = useBranding()
  // Keyed by the saved values, so the form restarts from them after a save.
  return <BrandingForm key={`${branding.primaryColor ?? ''}|${branding.secondaryColor ?? ''}`} branding={branding} />
}

function BrandingForm({ branding }: { branding: Branding }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [primary, setPrimary] = useState(branding.primaryColor ?? '')
  const [secondary, setSecondary] = useState(branding.secondaryColor ?? '')
  const [errors, setErrors] = useState<{ primaryColor?: string; secondaryColor?: string; file?: string }>({})
  const fileInput = useRef<HTMLInputElement>(null)

  const refresh = (saved: Branding) => queryClient.setQueryData(brandingQueryKey, saved)

  const saveColors = useMutation({
    mutationFn: () => updateBranding({ primaryColor: primary.trim(), secondaryColor: secondary.trim() }),
    onSuccess: (saved) => {
      setErrors({})
      refresh(saved)
      toast.success(t('branding.saved'))
    },
    onError: (caught) => {
      const problem = isApiError(caught) && caught.status === 400 ? caught.problem?.errors : undefined
      setErrors({ primaryColor: problem?.primaryColor?.[0], secondaryColor: problem?.secondaryColor?.[0] })
    },
  })

  const upload = useMutation({
    mutationFn: (file: File) => uploadBrandingLogo(file),
    onSuccess: (saved) => {
      setErrors({})
      refresh(saved)
      toast.success(t('branding.logoSaved'))
    },
    onError: (caught) => {
      const problem = isApiError(caught) && caught.status === 400 ? caught.problem?.errors : undefined
      setErrors({ file: problem?.file?.[0] })
    },
  })

  const remove = useMutation({
    mutationFn: removeBrandingLogo,
    onSuccess: (saved) => {
      refresh(saved)
      toast.success(t('branding.logoRemoved'))
    },
  })

  function onFile(file: File | undefined) {
    if (!file) return
    if (file.size > MAX_LOGO_BYTES) {
      setErrors({ file: t('branding.logoTooLarge') })
      return
    }
    upload.mutate(file)
    if (fileInput.current) fileInput.current.value = ''
  }

  const swatch = (value: string) => (parseHexColor(value) ? value : undefined)

  return (
    <div className="flex max-w-2xl flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold">{t('nav.branding')}</h1>
        <p className="text-muted-foreground">{t('branding.description')}</p>
      </div>

      <form
        noValidate
        className="flex flex-col gap-4"
        onSubmit={(event) => {
          event.preventDefault()
          saveColors.mutate()
        }}
      >
        <FieldGroup>
          <ColorField
            id="branding-primary"
            label={t('branding.primaryColor')}
            value={primary}
            onChange={setPrimary}
            error={errors.primaryColor}
            swatch={swatch(primary)}
          />
          <ColorField
            id="branding-secondary"
            label={t('branding.secondaryColor')}
            value={secondary}
            onChange={setSecondary}
            error={errors.secondaryColor}
            swatch={swatch(secondary)}
          />
        </FieldGroup>
        <div className="flex flex-wrap gap-2">
          <Button type="submit" disabled={saveColors.isPending}>
            {saveColors.isPending ? t('branding.saving') : t('branding.save')}
          </Button>
          <Button
            type="button"
            variant="outline"
            onClick={() => {
              setPrimary('')
              setSecondary('')
            }}
          >
            {t('branding.resetColors')}
          </Button>
        </div>
      </form>

      <section aria-labelledby="branding-logo-heading" className="flex flex-col gap-3">
        <h2 id="branding-logo-heading" className="text-lg font-semibold">
          {t('branding.logo')}
        </h2>
        <div className="flex min-h-12 items-center rounded-md border p-3">
          <BrandLogo alt={t('branding.logoAlt')} className="h-12 w-auto max-w-60 object-contain" />
          {branding.logoUrl ? null : <span className="text-muted-foreground">{t('branding.noLogo')}</span>}
        </div>
        <Field data-invalid={errors.file ? true : undefined}>
          <FieldLabel htmlFor="branding-logo-file">{t('branding.logoFile')}</FieldLabel>
          <Input
            ref={fileInput}
            id="branding-logo-file"
            type="file"
            accept="image/png,image/jpeg,image/webp,image/gif"
            aria-invalid={errors.file ? true : undefined}
            disabled={upload.isPending}
            onChange={(event) => onFile(event.target.files?.[0])}
          />
          <FieldDescription>{t('branding.logoHint')}</FieldDescription>
          {errors.file ? <FieldError errors={[{ message: errors.file }]} /> : null}
        </Field>
        {branding.logoUrl ? (
          <div>
            <Button type="button" variant="outline" disabled={remove.isPending} onClick={() => remove.mutate()}>
              {t('branding.removeLogo')}
            </Button>
          </div>
        ) : null}
      </section>
    </div>
  )
}

interface ColorFieldProps {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
  error?: string
  swatch?: string
}

function ColorField({ id, label, value, onChange, error, swatch }: ColorFieldProps) {
  const { t } = useTranslation()
  return (
    <Field data-invalid={error ? true : undefined}>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      <div className="flex items-center gap-3">
        <Input
          id={id}
          dir="ltr"
          autoComplete="off"
          placeholder={t('branding.colorPlaceholder')}
          value={value}
          aria-invalid={error ? true : undefined}
          onChange={(event) => onChange(event.target.value)}
        />
        <span
          aria-hidden="true"
          className="size-9 shrink-0 rounded-md border"
          style={swatch ? { backgroundColor: swatch } : undefined}
        />
      </div>
      {error ? <FieldError errors={[{ message: error }]} /> : null}
    </Field>
  )
}

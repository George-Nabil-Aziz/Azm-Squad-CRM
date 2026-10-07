import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { WebFormReceipt } from '@/api/web-forms'
import { Card, CardContent, CardDescription, CardHeader } from '@/components/ui/card'
import { BrandLogo } from '@/features/branding/BrandLogo'
import { ContactForm } from '@/features/web-forms/ContactForm'
import { supportedLanguages, type Language } from '@/i18n/i18n'

/**
 * The embeddable contact form (CRM-55): public, no sign-in, meant for an iframe on the company website
 * (`/embed/contact`, optional `?lang=ar`). Shows the ticket number after the visitor sent it.
 */
export function ContactFormPage() {
  const { t, i18n } = useTranslation()
  const [receipt, setReceipt] = useState<WebFormReceipt | null>(null)

  useEffect(() => {
    const lang = new URLSearchParams(window.location.search).get('lang')
    // Only for this visit: the language the staff member saved in this browser (same origin) must stay as it is.
    if (lang && supportedLanguages.includes(lang as Language)) void i18n.changeLanguage(lang)
  }, [i18n])

  return (
    <main className="flex min-h-screen w-full items-start justify-center bg-background p-4 sm:items-center sm:p-6">
      <Card role="region" aria-label={t('webForms.form.title')} className="w-full max-w-lg">
        <CardHeader className="items-center text-center">
          <BrandLogo alt={t('app.name')} className="mb-1 h-12 w-auto max-w-48 object-contain" />
          <p className="text-sm font-medium text-muted-foreground">{t('app.name')}</p>
          <h1 className="text-2xl font-semibold">{t('webForms.form.title')}</h1>
          <CardDescription>{t('webForms.form.subtitle')}</CardDescription>
        </CardHeader>
        <CardContent>
          {receipt ? (
            <div role="status" className="flex flex-col items-center gap-3 rounded-lg border bg-muted/40 p-6 text-center">
              <p className="font-medium">{t('webForms.form.thanks')}</p>
              {receipt.number ? (
                <p>
                  {t('webForms.form.number')}{' '}
                  <span dir="ltr" className="font-semibold">
                    {receipt.number}
                  </span>
                </p>
              ) : null}
              <p className="text-sm text-muted-foreground">{t('webForms.form.emailSent')}</p>
            </div>
          ) : (
            <ContactForm onSubmitted={setReceipt} />
          )}
        </CardContent>
      </Card>
    </main>
  )
}

import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { WebFormReceipt } from '@/api/web-forms'
import { ContactForm } from '@/features/web-forms/ContactForm'
import { setLanguage, supportedLanguages, type Language } from '@/i18n/i18n'

/**
 * The embeddable contact form (CRM-55): public, no sign-in, meant for an iframe on the company website
 * (`/embed/contact`, optional `?lang=ar`). Shows the ticket number after the visitor sent it.
 */
export function ContactFormPage() {
  const { t } = useTranslation()
  const [receipt, setReceipt] = useState<WebFormReceipt | null>(null)

  useEffect(() => {
    const lang = new URLSearchParams(window.location.search).get('lang')
    if (lang && supportedLanguages.includes(lang as Language)) void setLanguage(lang as Language)
  }, [])

  return (
    <main className="mx-auto flex w-full max-w-xl flex-col gap-4 p-4">
      <h1 className="text-2xl font-semibold text-primary">{t('webForms.form.title')}</h1>
      {receipt ? (
        <div role="status" className="flex flex-col gap-3 rounded-lg border p-4">
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
    </main>
  )
}

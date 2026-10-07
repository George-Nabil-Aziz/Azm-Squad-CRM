import { ExternalLinkIcon } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'

/** Admin page of the web forms (CRM-55): the iframe snippet to paste into the company website, with a live preview. */
export function WebFormsPage() {
  const { t } = useTranslation()
  const [copied, setCopied] = useState(false)
  const url = `${window.location.origin}/embed/contact`
  const attributes: [string, string][] = [
    ['src', url],
    ['title', t('webForms.form.title')],
    ['width', '100%'],
    ['height', '640'],
    ['style', 'border:0'],
  ]
  const snippet = `<iframe ${attributes.map(([name, value]) => `${name}="${value}"`).join(' ')}></iframe>`

  async function copy() {
    try {
      await navigator.clipboard.writeText(snippet)
      setCopied(true)
    } catch {
      setCopied(false) // clipboard blocked: the text can still be selected by hand
    }
  }

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold text-primary">{t('nav.webForms')}</h1>
        <p className="text-muted-foreground">{t('webForms.admin.description')}</p>
      </div>
      <div className="grid min-w-0 gap-6 lg:grid-cols-2">
        <div className="flex min-w-0 flex-col gap-3">
          <label htmlFor="web-form-embed" className="text-sm font-medium">
            {t('webForms.admin.embedCode')}
          </label>
          <Textarea id="web-form-embed" readOnly rows={6} dir="ltr" value={snippet} />
          <div className="flex flex-wrap items-center gap-3">
            <Button type="button" onClick={copy}>
              {copied ? t('webForms.admin.copied') : t('webForms.admin.copy')}
            </Button>
            <a
              href="/embed/contact"
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-1 text-primary underline-offset-4 hover:underline"
            >
              {t('webForms.admin.open')}
              <ExternalLinkIcon className="size-4" aria-hidden="true" />
            </a>
          </div>
          <p className="text-sm text-muted-foreground">{t('webForms.admin.captchaHint')}</p>
        </div>
        <section aria-label={t('webForms.admin.previewTitle')} className="flex min-w-0 flex-col gap-2">
          <h2 className="text-sm font-medium">{t('webForms.admin.previewTitle')}</h2>
          <iframe
            src="/embed/contact"
            title={t('webForms.admin.previewLabel')}
            className="h-[640px] w-full rounded-xl border bg-background"
          />
          <p className="text-xs text-muted-foreground">{t('webForms.admin.previewHint')}</p>
        </section>
      </div>
    </div>
  )
}

import { LanguagesIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { setLanguage } from '@/i18n/i18n'

/** Icon button that switches between Arabic and English. Its label (language.switch) is the other language's own name. */
export function LanguageSwitcher() {
  const { t, i18n } = useTranslation()
  const otherLanguage = i18n.resolvedLanguage === 'ar' ? 'en' : 'ar'

  return (
    <Button
      type="button"
      variant="ghost"
      size="icon-sm"
      lang={otherLanguage}
      aria-label={t('language.switch')}
      title={t('language.switch')}
      onClick={() => void setLanguage(otherLanguage)}
    >
      <LanguagesIcon aria-hidden="true" />
    </Button>
  )
}

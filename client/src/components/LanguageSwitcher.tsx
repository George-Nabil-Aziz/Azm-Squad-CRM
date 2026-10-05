import { LanguagesIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { setLanguage } from '@/i18n/i18n'

/** Switches between Arabic and English. The label (language.switch) is the other language's own name. */
export function LanguageSwitcher() {
  const { t, i18n } = useTranslation()
  const otherLanguage = i18n.resolvedLanguage === 'ar' ? 'en' : 'ar'

  return (
    <Button variant="ghost" size="sm" lang={otherLanguage} onClick={() => void setLanguage(otherLanguage)}>
      <LanguagesIcon aria-hidden="true" />
      {t('language.switch')}
    </Button>
  )
}

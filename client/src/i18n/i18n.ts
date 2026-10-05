import i18next from 'i18next'
import { initReactI18next } from 'react-i18next'
import ar from './ar.json'
import en from './en.json'

/** UI languages. The first one is the default. The API supports the same list (Accept-Language). */
export const supportedLanguages = ['en', 'ar'] as const
export type Language = (typeof supportedLanguages)[number]

const DEFAULT_LANGUAGE: Language = 'en'
const STORAGE_KEY = 'crm.language'

function isLanguage(value: unknown): value is Language {
  return supportedLanguages.includes(value as Language)
}

/** The language saved by setLanguage (survives a reload), or English. */
function readStoredLanguage(): Language {
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    return isLanguage(stored) ? stored : DEFAULT_LANGUAGE
  } catch {
    return DEFAULT_LANGUAGE
  }
}

/** The app's i18next instance (own instance, so tests can load a fresh one with vi.resetModules()). */
export const i18n = i18next.createInstance()

// <html lang dir> always follows the current language, including the initial one (CSS, fonts, shadcn RTL variants).
i18n.on('languageChanged', (language) => {
  document.documentElement.lang = language
  document.documentElement.dir = i18n.dir(language)
})

void i18n.use(initReactI18next).init({
  resources: { en: { translation: en }, ar: { translation: ar } },
  lng: readStoredLanguage(),
  fallbackLng: DEFAULT_LANGUAGE,
  supportedLngs: supportedLanguages,
  // Translations are bundled: initialise synchronously, so the first render already has the right language.
  initAsync: false,
  // React escapes rendered text already.
  interpolation: { escapeValue: false },
})

/** The current UI language. */
export function getLanguage(): Language {
  return isLanguage(i18n.resolvedLanguage) ? i18n.resolvedLanguage : DEFAULT_LANGUAGE
}

/** Switches the UI language and remembers the choice for the next visit. */
export async function setLanguage(language: Language): Promise<void> {
  try {
    localStorage.setItem(STORAGE_KEY, language)
  } catch {
    // Storage blocked (private mode): the switch still works for this visit.
  }
  await i18n.changeLanguage(language)
}

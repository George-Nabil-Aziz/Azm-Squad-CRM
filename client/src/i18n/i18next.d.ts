import 'i18next'
import type en from './en.json'

// Typed translation keys: t('auth.signIn') compiles, t('auth.typo') fails `npm run build` (tsc -b).
declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'translation'
    resources: {
      translation: typeof en
    }
  }
}

import { MonitorIcon, MoonIcon, SunIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { THEME_PREFERENCES, useTheme, type ThemePreference } from './theme-context'

const icons = { light: SunIcon, dark: MoonIcon, system: MonitorIcon } satisfies Record<ThemePreference, unknown>

/** Light / Dark / System switch (a radio group of three icon buttons). */
export function ThemeToggle() {
  const { t } = useTranslation()
  const { theme, setTheme } = useTheme()

  return (
    <div
      role="radiogroup"
      aria-label={t('theme.label')}
      className="inline-flex items-center gap-0.5 rounded-lg border bg-muted/50 p-0.5"
    >
      {THEME_PREFERENCES.map((option) => {
        const Icon = icons[option]
        const selected = theme === option
        return (
          <button
            key={option}
            type="button"
            role="radio"
            aria-checked={selected}
            aria-label={t(`theme.${option}`)}
            title={t(`theme.${option}`)}
            onClick={() => setTheme(option)}
            className={cn(
              'inline-flex size-6 items-center justify-center rounded-md text-muted-foreground transition-colors outline-none hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring/50',
              selected && 'bg-background text-foreground shadow-sm',
            )}
          >
            <Icon aria-hidden="true" className="size-3.5" />
          </button>
        )
      })}
    </div>
  )
}

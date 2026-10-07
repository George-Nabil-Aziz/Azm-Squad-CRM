import { ArrowRightIcon, CircleCheckIcon, ClockIcon, MessagesSquareIcon, StarIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { LanguageSwitcher } from '@/components/LanguageSwitcher'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { BrandLogo } from '@/features/branding/BrandLogo'
import { ThemeToggle } from '@/features/theme/ThemeToggle'

const YEAR = new Date().getFullYear()

const steps = [
  { id: 'reach', icon: MessagesSquareIcon },
  { id: 'resolve', icon: CircleCheckIcon },
  { id: 'rate', icon: StarIcon },
] as const

/** Public landing page (/welcome, and / for visitors who are not signed in). */
export function LandingPage() {
  const { t } = useTranslation()

  return (
    <div className="flex min-h-svh flex-col bg-background">
      <header className="border-b">
        <div className="mx-auto flex max-w-6xl items-center gap-2 px-4 py-3">
          <div className="flex items-center gap-2 text-lg font-semibold">
            <BrandLogo alt="" />
            {t('app.name')}
          </div>
          <div className="ms-auto flex items-center gap-1">
            <LanguageSwitcher />
            <ThemeToggle />
          </div>
        </div>
      </header>

      <main className="flex-1">
        <section className="mx-auto grid max-w-6xl items-center gap-10 px-4 py-12 md:grid-cols-2 md:py-20">
          <div className="space-y-6">
            <h1 className="text-3xl font-semibold tracking-tight text-balance sm:text-4xl lg:text-5xl">{t('landing.headline')}</h1>
            <p className="max-w-prose text-base text-muted-foreground sm:text-lg">{t('landing.subtitle')}</p>
            <div className="flex flex-wrap gap-3">
              <Button size="lg" asChild>
                <Link to="/login">
                  {t('auth.signIn')}
                  <ArrowRightIcon aria-hidden="true" className="rtl:rotate-180" />
                </Link>
              </Button>
              <Button size="lg" variant="outline" asChild>
                <Link to="/portal">{t('landing.portal')}</Link>
              </Button>
            </div>
          </div>

          {/* Decorative sample ticket. */}
          <div aria-hidden="true" className="mx-auto w-full max-w-md">
            <Card className="shadow-lg">
              <CardHeader>
                <div className="flex items-center justify-between gap-2">
                  <Badge variant="secondary">{t('landing.sample.channel')}</Badge>
                  <Badge variant="outline">{t('landing.sample.status')}</Badge>
                </div>
                <CardTitle className="pt-2">{t('landing.sample.subject')}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3 text-sm">
                <p className="rounded-lg bg-muted p-3 text-muted-foreground">{t('landing.sample.message')}</p>
                <p className="flex items-center gap-2 text-success">
                  <ClockIcon className="size-4" />
                  {t('landing.sample.sla')}
                </p>
              </CardContent>
            </Card>
          </div>
        </section>

        <section className="border-t bg-surface">
          <div className="mx-auto max-w-6xl px-4 py-12 md:py-16">
            <h2 className="text-2xl font-semibold tracking-tight">{t('landing.how.title')}</h2>
            <ol className="mt-8 grid gap-6 md:grid-cols-3">
              {steps.map(({ id, icon: Icon }, index) => (
                <li key={id} className="flex gap-4">
                  <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-accent text-accent-foreground">
                    <Icon aria-hidden="true" className="size-5" />
                  </span>
                  <div>
                    <h3 className="font-medium">
                      {index + 1}. {t(`landing.how.${id}.title`)}
                    </h3>
                    <p className="mt-1 text-sm text-muted-foreground">{t(`landing.how.${id}.text`)}</p>
                  </div>
                </li>
              ))}
            </ol>
          </div>
        </section>
      </main>

      <footer className="border-t">
        <div className="mx-auto max-w-6xl px-4 py-6 text-sm text-muted-foreground">
          © {YEAR} {t('app.name')}
        </div>
      </footer>
    </div>
  )
}

import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { getDemoAccounts, type DemoAccount } from '@/api/auth'
import type { RoleName } from '@/api/users'
import { Button } from '@/components/ui/button'

const isCustomer = (role: string) => role.toLowerCase() === 'customer'

/**
 * Development-only shortcut list: the server answers 404 outside Development, and then nothing is shown.
 * Picking an account hands it to the form: email and password for staff, email only for the portal customer.
 */
export function DemoAccounts({ audience, onPick }: { audience: 'staff' | 'customer'; onPick: (account: DemoAccount) => void }) {
  const { t } = useTranslation()
  const query = useQuery({
    queryKey: ['demo-accounts'],
    queryFn: ({ signal }) => getDemoAccounts(signal),
    retry: false,
  })
  const accounts = (Array.isArray(query.data) ? query.data : []).filter((a) =>
    audience === 'customer' ? isCustomer(a.role) : !isCustomer(a.role),
  )
  if (accounts.length === 0) return null

  return (
    <section aria-label={t('auth.demo.title')} className="mt-4 space-y-2 border-t pt-4">
      <h2 className="text-sm font-medium">{t('auth.demo.title')}</h2>
      <p className="text-xs text-muted-foreground">{t(audience === 'customer' ? 'auth.demo.customerHint' : 'auth.demo.staffHint')}</p>
      <ul className="space-y-1">
        {accounts.map((account) => (
          <li key={account.email}>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="h-auto w-full justify-between gap-2 py-1"
              onClick={() => onPick(account)}
            >
              <span dir="ltr">{account.email}</span>
              <span className="text-xs text-muted-foreground">
                {isCustomer(account.role) ? t('auth.demo.customer') : t(`users.roleNames.${account.role as RoleName}`)}
              </span>
            </Button>
          </li>
        ))}
      </ul>
    </section>
  )
}

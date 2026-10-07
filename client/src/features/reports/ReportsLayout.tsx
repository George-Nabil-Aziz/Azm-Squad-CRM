import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { NavLink, Outlet } from 'react-router'
import { Field, FieldLabel } from '@/components/ui/field'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { useBranches } from '@/features/branches/useBranches'
import { cn } from '@/lib/utils'
import { ReportBranchContext } from './report-branch'

/** Sub navigation of the reports area; every report is a child route. */
const reportLinks = [
  { id: 'dashboard', path: '/reports/dashboard' },
  { id: 'tickets', path: '/reports/tickets' },
  { id: 'sla', path: '/reports/sla' },
  { id: 'agents', path: '/reports/agents' },
  { id: 'satisfaction', path: '/reports/satisfaction' },
] as const

export function ReportsLayout() {
  const { t } = useTranslation()
  const [branchId, setBranchId] = useState('')
  const branches = useBranches({})

  return (
    <ReportBranchContext.Provider value={branchId || undefined}>
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold">{t('nav.reports')}</h1>
        <p className="text-muted-foreground">{t('reports.description')}</p>
      </div>
      <nav aria-label={t('reports.navigation')} className="flex flex-wrap gap-1 border-b">
        {reportLinks.map((link) => (
          <NavLink
            key={link.id}
            to={link.path}
            className={({ isActive }) =>
              cn(
                'border-b-2 px-3 py-2 text-sm font-medium',
                isActive ? 'border-primary text-foreground' : 'border-transparent text-muted-foreground hover:text-foreground',
              )
            }
          >
            {t(`reports.nav.${link.id}`)}
          </NavLink>
        ))}
      </nav>
      {branches.data && branches.data.length > 0 ? (
        <Field className="max-w-xs">
          <FieldLabel htmlFor="report-branch">{t('reports.filters.branch')}</FieldLabel>
          <NativeSelect id="report-branch" className="w-full" value={branchId} onChange={(event) => setBranchId(event.target.value)}>
            <NativeSelectOption value="">{t('reports.filters.allBranches')}</NativeSelectOption>
            {branches.data.map((branch) => (
              <NativeSelectOption key={branch.id} value={branch.id}>
                {branch.name}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        </Field>
      ) : null}
      <Outlet />
    </div>
    </ReportBranchContext.Provider>
  )
}

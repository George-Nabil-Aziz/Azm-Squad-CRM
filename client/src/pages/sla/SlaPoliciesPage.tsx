import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { SlaPolicy } from '@/api/sla-policies'
import { SlaPoliciesTable } from '@/features/sla/SlaPoliciesTable'
import { SlaPolicyFormDialog } from '@/features/sla/SlaPolicyFormDialog'
import { useSlaPolicies } from '@/features/sla/useSlaPolicies'

/** SLA settings (SuperAdmin): response and resolution time per priority. */
export function SlaPoliciesPage() {
  const { t } = useTranslation()
  const [editing, setEditing] = useState<SlaPolicy | null>(null)
  const policies = useSlaPolicies()

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold">{t('nav.slaPolicies')}</h1>
        <p className="text-muted-foreground">{t('sla.description')}</p>
      </div>

      {policies.isPending ? (
        <p className="text-muted-foreground">{t('sla.loading')}</p>
      ) : policies.data ? (
        <SlaPoliciesTable policies={policies.data} onEdit={setEditing} />
      ) : null}

      {editing ? <SlaPolicyFormDialog key={editing.priority} policy={editing} onClose={() => setEditing(null)} /> : null}
    </div>
  )
}

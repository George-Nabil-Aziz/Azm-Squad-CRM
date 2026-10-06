import { useTranslation } from 'react-i18next'
import type { SlaPolicy } from '@/api/sla-policies'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Can } from '@/features/auth/Can'
import { formatMinutes } from './sla-format'

interface SlaPoliciesTableProps {
  policies: SlaPolicy[]
  onEdit: (policy: SlaPolicy) => void
}

export function SlaPoliciesTable({ policies, onEdit }: SlaPoliciesTableProps) {
  const { t } = useTranslation()

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('sla.columns.priority')}</TableHead>
          <TableHead>{t('sla.columns.response')}</TableHead>
          <TableHead>{t('sla.columns.resolution')}</TableHead>
          <TableHead className="text-end">{t('sla.columns.actions')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {policies.map((policy) => (
          <TableRow key={policy.priority}>
            <TableCell className="font-medium">{t(`tickets.priorities.${policy.priority}`)}</TableCell>
            <TableCell>{formatMinutes(policy.responseMinutes, t)}</TableCell>
            <TableCell>{formatMinutes(policy.resolutionMinutes, t)}</TableCell>
            <TableCell>
              <div className="flex justify-end gap-2">
                <Can permission={permissions.slaManage}>
                  <Button variant="outline" size="sm" onClick={() => onEdit(policy)}>
                    {t('sla.edit')}
                  </Button>
                </Can>
              </div>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}

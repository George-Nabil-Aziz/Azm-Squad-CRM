import { useTranslation } from 'react-i18next'
import type { Branch } from '@/api/branches'
import { permissions } from '@/auth/permissions'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Can } from '@/features/auth/Can'

interface BranchesTableProps {
  branches: Branch[]
  onEdit: (branch: Branch) => void
}

export function BranchesTable({ branches, onEdit }: BranchesTableProps) {
  const { t } = useTranslation()

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('branches.columns.name')}</TableHead>
          <TableHead>{t('branches.columns.status')}</TableHead>
          <TableHead className="text-end">{t('branches.columns.actions')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {branches.map((branch) => (
          <TableRow key={branch.id}>
            <TableCell className="font-medium">{branch.name}</TableCell>
            <TableCell>
              <Badge variant={branch.isActive ? 'secondary' : 'outline'}>
                {t(branch.isActive ? 'branches.active' : 'branches.inactive')}
              </Badge>
            </TableCell>
            <TableCell>
              <div className="flex flex-wrap justify-end gap-2">
                <Can permission={permissions.branchesManage}>
                  <Button variant="outline" size="sm" onClick={() => onEdit(branch)}>
                    {t('branches.edit')}
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

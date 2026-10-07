import { useTranslation } from 'react-i18next'
import type { Department } from '@/api/departments'
import { permissions } from '@/auth/permissions'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Can } from '@/features/auth/Can'

interface DepartmentsTableProps {
  departments: Department[]
  onEdit: (department: Department) => void
  onEditSla: (department: Department) => void
}

export function DepartmentsTable({ departments, onEdit, onEditSla }: DepartmentsTableProps) {
  const { t } = useTranslation()

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('departments.columns.name')}</TableHead>
          <TableHead>{t('departments.columns.status')}</TableHead>
          <TableHead className="text-end">{t('departments.columns.actions')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {departments.map((department) => (
          <TableRow key={department.id}>
            <TableCell className="font-medium">{department.name}</TableCell>
            <TableCell>
              <Badge variant={department.isActive ? 'success' : 'outline'}>
                {t(department.isActive ? 'departments.active' : 'departments.inactive')}
              </Badge>
            </TableCell>
            <TableCell>
              <div className="flex flex-wrap justify-end gap-2">
                <Can permission={permissions.departmentsManage}>
                  <Button variant="outline" size="sm" onClick={() => onEdit(department)}>
                    {t('departments.edit')}
                  </Button>
                </Can>
                <Can permission={permissions.slaManage}>
                  <Button variant="outline" size="sm" onClick={() => onEditSla(department)}>
                    {t('departments.sla')}
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

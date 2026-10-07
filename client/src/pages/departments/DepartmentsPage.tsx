import { PlusIcon } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { Department } from '@/api/departments'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Can } from '@/features/auth/Can'
import { DepartmentFormDialog } from '@/features/departments/DepartmentFormDialog'
import { DepartmentSlaDialog } from '@/features/departments/DepartmentSlaDialog'
import { DepartmentsTable } from '@/features/departments/DepartmentsTable'
import { useDepartments } from '@/features/departments/useDepartments'

type DialogState =
  | { mode: 'create' }
  | { mode: 'edit'; department: Department }
  | { mode: 'sla'; department: Department }
  | null

/** Departments admin page: every department (active and inactive), add, edit / (de)activate, SLA overrides. */
export function DepartmentsPage() {
  const { t } = useTranslation()
  const [dialog, setDialog] = useState<DialogState>(null)
  const departments = useDepartments({})

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-semibold">{t('nav.departments')}</h1>
          <p className="text-muted-foreground">{t('departments.description')}</p>
        </div>
        <Can permission={permissions.departmentsManage}>
          <Button onClick={() => setDialog({ mode: 'create' })}>
            <PlusIcon aria-hidden="true" />
            {t('departments.add')}
          </Button>
        </Can>
      </div>

      {departments.isPending ? (
        <p className="text-muted-foreground">{t('departments.loading')}</p>
      ) : departments.data && departments.data.length > 0 ? (
        <DepartmentsTable
          departments={departments.data}
          onEdit={(department) => setDialog({ mode: 'edit', department })}
          onEditSla={(department) => setDialog({ mode: 'sla', department })}
        />
      ) : (
        <p className="text-muted-foreground">{t('departments.empty')}</p>
      )}

      {dialog?.mode === 'sla' ? (
        <DepartmentSlaDialog key={dialog.department.id} department={dialog.department} onClose={() => setDialog(null)} />
      ) : dialog ? (
        <DepartmentFormDialog
          key={dialog.mode === 'edit' ? dialog.department.id : 'new'}
          department={dialog.mode === 'edit' ? dialog.department : undefined}
          onClose={() => setDialog(null)}
        />
      ) : null}
    </div>
  )
}

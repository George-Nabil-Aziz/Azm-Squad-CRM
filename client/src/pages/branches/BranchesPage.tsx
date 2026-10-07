import { PlusIcon } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { Branch } from '@/api/branches'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Can } from '@/features/auth/Can'
import { BranchFormDialog } from '@/features/branches/BranchFormDialog'
import { BranchesTable } from '@/features/branches/BranchesTable'
import { useBranches } from '@/features/branches/useBranches'

type DialogState =
  | { mode: 'create' }
  | { mode: 'edit'; branch: Branch }
  | null

/** Branches admin page: every branch (active and inactive), add, edit / (de)activate. */
export function BranchesPage() {
  const { t } = useTranslation()
  const [dialog, setDialog] = useState<DialogState>(null)
  const branches = useBranches({})

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-semibold text-primary">{t('nav.branches')}</h1>
          <p className="text-muted-foreground">{t('branches.description')}</p>
        </div>
        <Can permission={permissions.branchesManage}>
          <Button onClick={() => setDialog({ mode: 'create' })}>
            <PlusIcon aria-hidden="true" />
            {t('branches.add')}
          </Button>
        </Can>
      </div>

      {branches.isPending ? (
        <p className="text-muted-foreground">{t('branches.loading')}</p>
      ) : branches.data && branches.data.length > 0 ? (
        <BranchesTable
          branches={branches.data}
          onEdit={(branch) => setDialog({ mode: 'edit', branch })}
        />
      ) : (
        <p className="text-muted-foreground">{t('branches.empty')}</p>
      )}

      {dialog ? (
        <BranchFormDialog
          key={dialog.mode === 'edit' ? dialog.branch.id : 'new'}
          branch={dialog.mode === 'edit' ? dialog.branch : undefined}
          onClose={() => setDialog(null)}
        />
      ) : null}
    </div>
  )
}

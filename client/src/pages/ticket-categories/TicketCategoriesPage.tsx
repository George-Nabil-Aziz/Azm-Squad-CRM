import { PlusIcon } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { TicketCategory } from '@/api/ticket-categories'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Can } from '@/features/auth/Can'
import { TicketCategoriesTable } from '@/features/ticket-categories/TicketCategoriesTable'
import { TicketCategoryFormDialog } from '@/features/ticket-categories/TicketCategoryFormDialog'
import { useTicketCategories } from '@/features/ticket-categories/useTicketCategories'

type DialogState = { mode: 'create' } | { mode: 'edit'; category: TicketCategory } | null

/** Ticket categories admin page: every category (active and inactive), add, edit / (de)activate. */
export function TicketCategoriesPage() {
  const { t } = useTranslation()
  const [dialog, setDialog] = useState<DialogState>(null)
  const categories = useTicketCategories({})

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-semibold text-primary">{t('nav.ticketCategories')}</h1>
          <p className="text-muted-foreground">{t('ticketCategories.description')}</p>
        </div>
        <Can permission={permissions.categoriesManage}>
          <Button onClick={() => setDialog({ mode: 'create' })}>
            <PlusIcon aria-hidden="true" />
            {t('ticketCategories.add')}
          </Button>
        </Can>
      </div>

      {categories.isPending ? (
        <p className="text-muted-foreground">{t('ticketCategories.loading')}</p>
      ) : categories.data && categories.data.length > 0 ? (
        <TicketCategoriesTable categories={categories.data} onEdit={(category) => setDialog({ mode: 'edit', category })} />
      ) : (
        <p className="text-muted-foreground">{t('ticketCategories.empty')}</p>
      )}

      {dialog ? (
        <TicketCategoryFormDialog
          key={dialog.mode === 'edit' ? dialog.category.id : 'new'}
          category={dialog.mode === 'edit' ? dialog.category : undefined}
          onClose={() => setDialog(null)}
        />
      ) : null}
    </div>
  )
}

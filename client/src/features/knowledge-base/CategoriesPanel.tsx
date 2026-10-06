import { useMutation, useQueryClient } from '@tanstack/react-query'
import { PlusIcon } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { deleteKbCategory, type KbCategory } from '@/api/knowledge-base'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Can } from '@/features/auth/Can'
import { CategoryFormDialog } from './CategoryFormDialog'
import { ConfirmDeleteButton } from './ConfirmDeleteButton'
import { kbQueryKey, useKbCategories } from './useKnowledgeBase'

type DialogState = { mode: 'create' } | { mode: 'edit'; category: KbCategory } | null

function CategoryActions({ category, onEdit }: { category: KbCategory; onEdit: (category: KbCategory) => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const remove = useMutation({
    mutationFn: () => deleteKbCategory(category.id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: kbQueryKey })
      toast.success(t('knowledgeBase.categoryDeleted', { name: category.name }))
    },
  })

  return (
    <div className="flex justify-end gap-2">
      <Button variant="outline" size="sm" onClick={() => onEdit(category)}>
        {t('knowledgeBase.edit')}
      </Button>
      <ConfirmDeleteButton
        title={t('knowledgeBase.deleteCategoryTitle', { name: category.name })}
        description={t('knowledgeBase.deleteCategoryDescription')}
        disabled={remove.isPending}
        onConfirm={() => remove.mutate()}
      />
    </div>
  )
}

/** Categories tab: add, rename and delete (a category with articles cannot be deleted: the API answers 409). */
export function CategoriesPanel() {
  const { t } = useTranslation()
  const [dialog, setDialog] = useState<DialogState>(null)
  const categories = useKbCategories()

  return (
    <div className="flex flex-col gap-4">
      <Can permission={permissions.kbManage}>
        <div className="flex justify-end">
          <Button onClick={() => setDialog({ mode: 'create' })}>
            <PlusIcon aria-hidden="true" />
            {t('knowledgeBase.addCategory')}
          </Button>
        </div>
      </Can>

      {categories.isPending ? (
        <p className="text-muted-foreground">{t('knowledgeBase.loading')}</p>
      ) : categories.data && categories.data.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('knowledgeBase.columns.name')}</TableHead>
              <TableHead>{t('knowledgeBase.columns.articles')}</TableHead>
              <Can permission={permissions.kbManage}>
                <TableHead className="text-end">{t('knowledgeBase.columns.actions')}</TableHead>
              </Can>
            </TableRow>
          </TableHeader>
          <TableBody>
            {categories.data.map((category) => (
              <TableRow key={category.id}>
                <TableCell className="font-medium">{category.name}</TableCell>
                <TableCell>{category.articleCount}</TableCell>
                <Can permission={permissions.kbManage}>
                  <TableCell>
                    <CategoryActions category={category} onEdit={(selected) => setDialog({ mode: 'edit', category: selected })} />
                  </TableCell>
                </Can>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <p className="text-muted-foreground">{t('knowledgeBase.emptyCategories')}</p>
      )}

      {dialog ? (
        <CategoryFormDialog
          key={dialog.mode === 'edit' ? dialog.category.id : 'new'}
          category={dialog.mode === 'edit' ? dialog.category : undefined}
          onClose={() => setDialog(null)}
        />
      ) : null}
    </div>
  )
}

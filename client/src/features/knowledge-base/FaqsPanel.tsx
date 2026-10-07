import { useMutation, useQueryClient } from '@tanstack/react-query'
import { PlusIcon } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { deleteKbFaq, type KbFaq } from '@/api/knowledge-base'
import { permissions } from '@/auth/permissions'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Can } from '@/features/auth/Can'
import { ConfirmDeleteButton } from './ConfirmDeleteButton'
import { FaqFormDialog } from './FaqFormDialog'
import { kbQueryKey, useKbFaqs } from './useKnowledgeBase'

type DialogState = { mode: 'create' } | { mode: 'edit'; faq: KbFaq } | null

function FaqActions({ faq, onEdit }: { faq: KbFaq; onEdit: (faq: KbFaq) => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const remove = useMutation({
    mutationFn: () => deleteKbFaq(faq.id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: kbQueryKey })
      toast.success(t('knowledgeBase.faqDeleted', { question: faq.question }))
    },
  })

  return (
    <div className="flex justify-end gap-2">
      <Button variant="outline" size="sm" onClick={() => onEdit(faq)}>
        {t('knowledgeBase.edit')}
      </Button>
      <ConfirmDeleteButton
        title={t('knowledgeBase.deleteFaqTitle', { question: faq.question })}
        description={t('knowledgeBase.deleteFaqDescription')}
        disabled={remove.isPending}
        onConfirm={() => remove.mutate()}
      />
    </div>
  )
}

/** FAQs tab, ordered by display order. Editors also see the unpublished FAQs; everyone else only the published ones. */
export function FaqsPanel() {
  const { t } = useTranslation()
  const [dialog, setDialog] = useState<DialogState>(null)
  const faqs = useKbFaqs()

  return (
    <div className="flex flex-col gap-4">
      <Can permission={permissions.kbManage}>
        <div className="flex justify-end">
          <Button onClick={() => setDialog({ mode: 'create' })}>
            <PlusIcon aria-hidden="true" />
            {t('knowledgeBase.addFaq')}
          </Button>
        </div>
      </Can>

      {faqs.isPending ? (
        <p className="text-muted-foreground">{t('knowledgeBase.loading')}</p>
      ) : faqs.data && faqs.data.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('knowledgeBase.columns.order')}</TableHead>
              <TableHead>{t('knowledgeBase.columns.question')}</TableHead>
              <TableHead>{t('knowledgeBase.columns.status')}</TableHead>
              <Can permission={permissions.kbManage}>
                <TableHead className="text-end">{t('knowledgeBase.columns.actions')}</TableHead>
              </Can>
            </TableRow>
          </TableHeader>
          <TableBody>
            {faqs.data.map((faq) => (
              <TableRow key={faq.id}>
                <TableCell>{faq.displayOrder}</TableCell>
                <TableCell className="font-medium">{faq.question}</TableCell>
                <TableCell>
                  <Badge variant={faq.isPublished ? 'success' : 'outline'}>
                    {t(faq.isPublished ? 'knowledgeBase.statuses.published' : 'knowledgeBase.statuses.draft')}
                  </Badge>
                </TableCell>
                <Can permission={permissions.kbManage}>
                  <TableCell>
                    <FaqActions faq={faq} onEdit={(selected) => setDialog({ mode: 'edit', faq: selected })} />
                  </TableCell>
                </Can>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <p className="text-muted-foreground">{t('knowledgeBase.emptyFaqs')}</p>
      )}

      {dialog ? (
        <FaqFormDialog
          key={dialog.mode === 'edit' ? dialog.faq.id : 'new'}
          faq={dialog.mode === 'edit' ? dialog.faq : undefined}
          onClose={() => setDialog(null)}
        />
      ) : null}
    </div>
  )
}

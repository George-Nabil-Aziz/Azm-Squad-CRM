import { useMutation, useQueryClient } from '@tanstack/react-query'
import { PlusIcon } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import {
  deleteKbArticle,
  publishKbArticle,
  unpublishKbArticle,
  type KbArticle,
  type KbArticleStatus,
} from '@/api/knowledge-base'
import { permissions } from '@/auth/permissions'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Can } from '@/features/auth/Can'
import { usePermissions } from '@/features/auth/usePermissions'
import { ArticleFormDialog } from './ArticleFormDialog'
import { ConfirmDeleteButton } from './ConfirmDeleteButton'
import { kbQueryKey, useKbArticles, useKbCategories } from './useKnowledgeBase'

type DialogState = { mode: 'create' } | { mode: 'edit'; article: KbArticle } | null

const PAGE_SIZE = 20

function ArticleActions({ article, onEdit }: { article: KbArticle; onEdit: (article: KbArticle) => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const refresh = () => queryClient.invalidateQueries({ queryKey: kbQueryKey })
  const toggle = useMutation({
    mutationFn: () => (article.status === 'published' ? unpublishKbArticle(article.id) : publishKbArticle(article.id)),
    onSuccess: async (saved) => {
      await refresh()
      toast.success(t(saved.status === 'published' ? 'knowledgeBase.published' : 'knowledgeBase.unpublished', { title: saved.title }))
    },
  })
  const remove = useMutation({
    mutationFn: () => deleteKbArticle(article.id),
    onSuccess: async () => {
      await refresh()
      toast.success(t('knowledgeBase.articleDeleted', { title: article.title }))
    },
  })

  return (
    <div className="flex justify-end gap-2">
      <Button variant="outline" size="sm" disabled={toggle.isPending} onClick={() => toggle.mutate()}>
        {t(article.status === 'published' ? 'knowledgeBase.unpublish' : 'knowledgeBase.publish')}
      </Button>
      <Button variant="outline" size="sm" onClick={() => onEdit(article)}>
        {t('knowledgeBase.edit')}
      </Button>
      <ConfirmDeleteButton
        title={t('knowledgeBase.deleteArticleTitle', { title: article.title })}
        description={t('knowledgeBase.deleteArticleDescription')}
        disabled={remove.isPending}
        onConfirm={() => remove.mutate()}
      />
    </div>
  )
}

/**
 * Articles tab. Editors (kb.manage) see drafts too, filter by status and write; everyone else only gets the
 * published articles (the API enforces it).
 */
export function ArticlesPanel() {
  const { t } = useTranslation()
  const { can } = usePermissions()
  const canManage = can(permissions.kbManage)
  const [dialog, setDialog] = useState<DialogState>(null)
  const [categoryId, setCategoryId] = useState('')
  const [status, setStatus] = useState<KbArticleStatus | ''>('')
  const [page, setPage] = useState(1)
  const categories = useKbCategories()
  const articles = useKbArticles({
    categoryId: categoryId || undefined,
    status: status || undefined,
    page,
    pageSize: PAGE_SIZE,
  })
  const totalPages = Math.max(1, Math.ceil((articles.data?.totalCount ?? 0) / PAGE_SIZE))

  return (
    <div className="flex min-w-0 flex-col gap-4">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex min-w-0 flex-wrap gap-3">
          <NativeSelect
            aria-label={t('knowledgeBase.filterCategory')}
            value={categoryId}
            onChange={(event) => {
              setCategoryId(event.target.value)
              setPage(1)
            }}
          >
            <NativeSelectOption value="">{t('knowledgeBase.allCategories')}</NativeSelectOption>
            {categories.data?.map((category) => (
              <NativeSelectOption key={category.id} value={category.id}>
                {category.name}
              </NativeSelectOption>
            ))}
          </NativeSelect>
          <Can permission={permissions.kbManage}>
            <NativeSelect
              aria-label={t('knowledgeBase.filterStatus')}
              value={status}
              onChange={(event) => {
                setStatus(event.target.value as KbArticleStatus | '')
                setPage(1)
              }}
            >
              <NativeSelectOption value="">{t('knowledgeBase.allStatuses')}</NativeSelectOption>
              <NativeSelectOption value="draft">{t('knowledgeBase.statuses.draft')}</NativeSelectOption>
              <NativeSelectOption value="published">{t('knowledgeBase.statuses.published')}</NativeSelectOption>
            </NativeSelect>
          </Can>
        </div>
        <Can permission={permissions.kbManage}>
          <Button onClick={() => setDialog({ mode: 'create' })}>
            <PlusIcon aria-hidden="true" />
            {t('knowledgeBase.addArticle')}
          </Button>
        </Can>
      </div>

      {articles.isPending ? (
        <p className="text-muted-foreground">{t('knowledgeBase.loading')}</p>
      ) : articles.data && articles.data.items.length > 0 ? (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('knowledgeBase.columns.title')}</TableHead>
                <TableHead>{t('knowledgeBase.columns.category')}</TableHead>
                <TableHead>{t('knowledgeBase.columns.status')}</TableHead>
                <TableHead>{t('knowledgeBase.columns.helpful')}</TableHead>
                <TableHead>{t('knowledgeBase.columns.linked')}</TableHead>
                {canManage ? <TableHead className="text-end">{t('knowledgeBase.columns.actions')}</TableHead> : null}
              </TableRow>
            </TableHeader>
            <TableBody>
              {articles.data.items.map((article) => (
                <TableRow key={article.id}>
                  <TableCell className="font-medium">{article.title}</TableCell>
                  <TableCell>{article.categoryName}</TableCell>
                  <TableCell>
                    <Badge variant={article.status === 'published' ? 'success' : 'outline'}>
                      {t(`knowledgeBase.statuses.${article.status}`)}
                    </Badge>
                  </TableCell>
                  <TableCell>{`${article.helpfulCount} / ${article.notHelpfulCount}`}</TableCell>
                  <TableCell>{article.linkedCount}</TableCell>
                  {canManage ? (
                    <TableCell>
                      <ArticleActions article={article} onEdit={(selected) => setDialog({ mode: 'edit', article: selected })} />
                    </TableCell>
                  ) : null}
                </TableRow>
              ))}
            </TableBody>
          </Table>
          {totalPages > 1 ? (
            <div className="flex items-center justify-between gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                {t('knowledgeBase.previous')}
              </Button>
              <span className="text-sm text-muted-foreground">{t('knowledgeBase.pageOf', { page, total: totalPages })}</span>
              <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
                {t('knowledgeBase.next')}
              </Button>
            </div>
          ) : null}
        </>
      ) : (
        <p className="text-muted-foreground">{t('knowledgeBase.emptyArticles')}</p>
      )}

      {dialog ? (
        <ArticleFormDialog
          key={dialog.mode === 'edit' ? dialog.article.id : 'new'}
          article={dialog.mode === 'edit' ? dialog.article : undefined}
          onClose={() => setDialog(null)}
        />
      ) : null}
    </div>
  )
}

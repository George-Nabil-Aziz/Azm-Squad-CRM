import { useTranslation } from 'react-i18next'
import type { TicketCategory } from '@/api/ticket-categories'
import { permissions } from '@/auth/permissions'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Can } from '@/features/auth/Can'

interface TicketCategoriesTableProps {
  categories: TicketCategory[]
  onEdit: (category: TicketCategory) => void
}

export function TicketCategoriesTable({ categories, onEdit }: TicketCategoriesTableProps) {
  const { t } = useTranslation()

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('ticketCategories.columns.name')}</TableHead>
          <TableHead>{t('ticketCategories.columns.status')}</TableHead>
          <TableHead className="text-end">{t('ticketCategories.columns.actions')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {categories.map((category) => (
          <TableRow key={category.id}>
            <TableCell className="font-medium">{category.name}</TableCell>
            <TableCell>
              <Badge variant={category.isActive ? 'secondary' : 'outline'}>
                {t(category.isActive ? 'ticketCategories.active' : 'ticketCategories.inactive')}
              </Badge>
            </TableCell>
            <TableCell>
              <div className="flex justify-end gap-2">
                <Can permission={permissions.categoriesManage}>
                  <Button variant="outline" size="sm" onClick={() => onEdit(category)}>
                    {t('ticketCategories.edit')}
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

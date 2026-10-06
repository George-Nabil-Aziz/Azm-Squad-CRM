import { useTranslation } from 'react-i18next'
import type { Customer } from '@/api/customers'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Can } from '@/features/auth/Can'
import { DeleteCustomerAction } from './DeleteCustomerAction'

interface CustomersTableProps {
  customers: Customer[]
  onEdit: (customer: Customer) => void
}

export function CustomersTable({ customers, onEdit }: CustomersTableProps) {
  const { t } = useTranslation()

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('customers.columns.name')}</TableHead>
          <TableHead>{t('customers.columns.phone')}</TableHead>
          <TableHead>{t('customers.columns.email')}</TableHead>
          <Can permission={permissions.customersManage}>
            <TableHead className="text-end">{t('customers.columns.actions')}</TableHead>
          </Can>
        </TableRow>
      </TableHeader>
      <TableBody>
        {customers.map((customer) => (
          <TableRow key={customer.id}>
            <TableCell className="font-medium">{customer.name}</TableCell>
            {/* Phone numbers and emails read left to right in Arabic too. */}
            <TableCell dir="ltr" className="text-start">
              {customer.phone}
            </TableCell>
            <TableCell dir="ltr" className="text-start">
              {customer.email}
            </TableCell>
            <Can permission={permissions.customersManage}>
              <TableCell>
                <div className="flex justify-end gap-2">
                  <Button variant="outline" size="sm" onClick={() => onEdit(customer)}>
                    {t('customers.edit')}
                  </Button>
                  <DeleteCustomerAction customer={customer} />
                </div>
              </TableCell>
            </Can>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}

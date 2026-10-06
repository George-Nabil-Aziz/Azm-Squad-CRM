import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import type { Customer } from '@/api/customers'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Can } from '@/features/auth/Can'
import { DeleteCustomerAction } from './DeleteCustomerAction'

interface CustomersTableProps {
  customers: Customer[]
  onEdit: (customer: Customer) => void
  onContacts: (customer: Customer) => void
}

export function CustomersTable({ customers, onEdit, onContacts }: CustomersTableProps) {
  const { t } = useTranslation()

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('customers.columns.name')}</TableHead>
          <TableHead>{t('customers.columns.phone')}</TableHead>
          <TableHead>{t('customers.columns.email')}</TableHead>
          <TableHead className="text-end">{t('customers.columns.actions')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {customers.map((customer) => (
          <TableRow key={customer.id}>
            <TableCell className="font-medium">
              <Link to={`/customers/${customer.id}`} className="text-primary underline-offset-4 hover:underline">
                {customer.name}
              </Link>
            </TableCell>
            {/* Phone numbers and emails read left to right in Arabic too. */}
            <TableCell dir="ltr" className="text-start">
              {customer.phone}
            </TableCell>
            <TableCell dir="ltr" className="text-start">
              {customer.email}
            </TableCell>
            <TableCell>
              <div className="flex justify-end gap-2">
                {/* Everyone who sees customers sees their contacts; changing them needs customers.manage. */}
                <Button variant="outline" size="sm" onClick={() => onContacts(customer)}>
                  {t('customers.contactsButton')}
                </Button>
                <Can permission={permissions.customersManage}>
                  <Button variant="outline" size="sm" onClick={() => onEdit(customer)}>
                    {t('customers.edit')}
                  </Button>
                  <DeleteCustomerAction customer={customer} />
                </Can>
              </div>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}

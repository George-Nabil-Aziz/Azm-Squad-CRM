import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { makeCustomerContactPrimary, removeCustomerContact, type CustomerContact } from '@/api/customers'
import { permissions } from '@/auth/permissions'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Can } from '@/features/auth/Can'
import { customersQueryKey } from './useCustomers'

interface CustomerContactsTableProps {
  customerId: string
  contacts: CustomerContact[]
}

/** The customer's contacts, with make-primary and remove buttons for users who may manage customers. */
export function CustomerContactsTable({ customerId, contacts }: CustomerContactsTableProps) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const refresh = () => queryClient.invalidateQueries({ queryKey: customersQueryKey })

  const makePrimary = useMutation({
    mutationFn: (contactId: string) => makeCustomerContactPrimary(customerId, contactId),
    onSuccess: async () => {
      await refresh()
      toast.success(t('customers.contacts.primaryChanged'))
    },
  })
  const remove = useMutation({
    mutationFn: (contactId: string) => removeCustomerContact(customerId, contactId),
    onSuccess: async () => {
      await refresh()
      toast.success(t('customers.contacts.removed'))
    },
  })
  const busy = makePrimary.isPending || remove.isPending

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('customers.contacts.columns.type')}</TableHead>
          <TableHead>{t('customers.contacts.columns.value')}</TableHead>
          <TableHead>{t('customers.contacts.columns.primary')}</TableHead>
          <Can permission={permissions.customersManage}>
            <TableHead className="text-end">{t('customers.contacts.columns.actions')}</TableHead>
          </Can>
        </TableRow>
      </TableHeader>
      <TableBody>
        {contacts.map((contact) => (
          <TableRow key={contact.id}>
            <TableCell>{t(`customers.contacts.types.${contact.type}`)}</TableCell>
            {/* Numbers and emails read left to right in Arabic too. */}
            <TableCell dir="ltr" className="text-start">
              {contact.value}
            </TableCell>
            <TableCell>
              {contact.isPrimary ? <Badge variant="secondary">{t('customers.contacts.primary')}</Badge> : null}
            </TableCell>
            <Can permission={permissions.customersManage}>
              <TableCell>
                <div className="flex justify-end gap-2">
                  {contact.isPrimary ? null : (
                    <Button variant="outline" size="sm" disabled={busy} onClick={() => makePrimary.mutate(contact.id)}>
                      {t('customers.contacts.makePrimary')}
                    </Button>
                  )}
                  <Button variant="outline" size="sm" disabled={busy} onClick={() => remove.mutate(contact.id)}>
                    {t('customers.contacts.remove')}
                  </Button>
                </div>
              </TableCell>
            </Can>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}

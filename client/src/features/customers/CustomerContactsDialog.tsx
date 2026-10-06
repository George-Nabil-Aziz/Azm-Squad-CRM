import { useTranslation } from 'react-i18next'
import type { Customer } from '@/api/customers'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Can } from '@/features/auth/Can'
import { AddContactForm } from './AddContactForm'
import { CustomerContactsTable } from './CustomerContactsTable'
import { useCustomer } from './useCustomers'

interface CustomerContactsDialogProps {
  customer: Customer
  onClose: () => void
}

/**
 * Phones, emails and WhatsApp numbers of one customer. Loads the customer again (GET /api/customers/{id}), so every
 * change made here (they invalidate the customers queries) shows up at once.
 */
export function CustomerContactsDialog({ customer, onClose }: CustomerContactsDialogProps) {
  const { t } = useTranslation()
  const details = useCustomer(customer.id)
  const contacts = details.data?.contacts

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent showCloseButton={false} className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t('customers.contacts.title', { name: customer.name })}</DialogTitle>
          <DialogDescription>{t('customers.contacts.description')}</DialogDescription>
        </DialogHeader>
        {contacts === undefined ? (
          <p className="text-muted-foreground">{t('customers.contacts.loading')}</p>
        ) : contacts.length === 0 ? (
          <p className="text-muted-foreground">{t('customers.contacts.empty')}</p>
        ) : (
          <CustomerContactsTable customerId={customer.id} contacts={contacts} />
        )}
        <Can permission={permissions.customersManage}>
          <AddContactForm customerId={customer.id} />
        </Can>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={onClose}>
            {t('customers.contacts.close')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

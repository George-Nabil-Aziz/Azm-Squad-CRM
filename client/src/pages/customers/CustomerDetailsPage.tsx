import { ArrowLeftIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { isApiError } from '@/api/errors'
import { CustomerAttachments } from '@/features/customers/CustomerAttachments'
import { CustomerContactsTable } from '@/features/customers/CustomerContactsTable'
import { CustomerNotes } from '@/features/customers/CustomerNotes'
import { CustomerTimeline } from '@/features/customers/CustomerTimeline'
import { useCustomer } from '@/features/customers/useCustomers'

/** One customer: contacts, notes, attachments and the interaction history (route customers/:id). */
export function CustomerDetailsPage() {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const customer = useCustomer(id)

  return (
    <div className="flex flex-col gap-6">
      <Link
        to="/customers"
        className="flex w-fit items-center gap-1 text-sm text-primary underline-offset-4 hover:underline"
      >
        <ArrowLeftIcon aria-hidden="true" className="size-4 rtl:rotate-180" />
        {t('customers.details.back')}
      </Link>

      {customer.isPending ? (
        <p className="text-muted-foreground">{t('customers.details.loading')}</p>
      ) : customer.data ? (
        <>
          <h1 className="text-2xl font-semibold">{customer.data.name}</h1>
          <section className="flex flex-col gap-4">
            <h2 className="text-lg font-semibold">{t('customers.details.contacts')}</h2>
            {customer.data.contacts.length > 0 ? (
              <CustomerContactsTable customerId={id} contacts={customer.data.contacts} />
            ) : (
              <p className="text-muted-foreground">{t('customers.contacts.empty')}</p>
            )}
          </section>
          <CustomerNotes customerId={id} />
          <CustomerAttachments customerId={id} />
          <CustomerTimeline customerId={id} />
        </>
      ) : (
        <p className="text-muted-foreground">
          {isApiError(customer.error) && customer.error.status === 404
            ? t('customers.details.notFound')
            : t('errors.generic')}
        </p>
      )}
    </div>
  )
}

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { getCustomerErp, linkCustomerToErp, type ErpOrder } from '@/api/integrations'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Can } from '@/features/auth/Can'

function formatDate(value: string | null) {
  return value ? new Date(value).toLocaleDateString() : ''
}

function formatTotal(row: ErpOrder) {
  return row.total === null ? '' : `${row.total.toLocaleString()} ${row.currency ?? ''}`.trim()
}

/** The customer's ERP data (CRM-60), read only: link by ERP id, recent orders and invoices, a message when the ERP is down. */
export function ErpPanel({ customerId }: { customerId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const queryKey = ['customer-erp', customerId] as const
  const erp = useQuery({ queryKey, queryFn: ({ signal }) => getCustomerErp(customerId, signal) })
  const [erpId, setErpId] = useState('')
  const link = useMutation({
    mutationFn: (id: string) => linkCustomerToErp(customerId, id),
    onSuccess: async () => {
      setErpId('')
      await queryClient.invalidateQueries({ queryKey })
      await queryClient.invalidateQueries({ queryKey: ['customers'] })
    },
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    if (erpId.trim()) link.mutate(erpId)
  }

  const data = erp.data

  return (
    <section aria-label={t('erp.title')} className="flex flex-col gap-4">
      <h2 className="text-lg font-semibold">{t('erp.title')}</h2>
      {erp.isPending ? <p className="text-muted-foreground">{t('erp.loading')}</p> : null}
      {erp.isError ? <p role="alert" className="text-sm text-destructive">{t('erp.unavailable')}</p> : null}

      {data && !data.linked ? <p className="text-muted-foreground">{t('erp.notLinked')}</p> : null}
      {data?.linked ? (
        <div className="flex flex-wrap items-center gap-2">
          <p dir="auto" className="text-sm">{t('erp.linkedAs', { id: data.erpCustomerId })}</p>
          <Can permission={permissions.customersManage}>
            <Button type="button" variant="outline" size="sm" disabled={link.isPending} onClick={() => link.mutate('')}>
              {t('erp.unlink')}
            </Button>
          </Can>
        </div>
      ) : null}

      {data && !data.linked ? (
        <Can permission={permissions.customersManage}>
          <form onSubmit={submit} className="flex max-w-md items-end gap-2">
            <div className="flex flex-1 flex-col gap-1">
              <label htmlFor="erp-customer-id" className="text-sm font-medium">
                {t('erp.linkLabel')}
              </label>
              <Input id="erp-customer-id" dir="ltr" value={erpId} onChange={(e) => setErpId(e.target.value)} />
            </div>
            <Button type="submit" disabled={link.isPending}>
              {t('erp.link')}
            </Button>
          </form>
        </Can>
      ) : null}

      {data?.linked && !data.available ? (
        <p role="alert" className="rounded-md border p-3 text-sm">
          {data.message ?? t('erp.unavailable')}
        </p>
      ) : null}

      {data?.linked && data.available ? (
        <>
          <h3 className="font-medium">{t('erp.orders')}</h3>
          {data.orders.length === 0 ? (
            <p className="text-muted-foreground">{t('erp.noOrders')}</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('erp.number')}</TableHead>
                  <TableHead>{t('erp.date')}</TableHead>
                  <TableHead>{t('erp.status')}</TableHead>
                  <TableHead>{t('erp.total')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.orders.map((order) => (
                  <TableRow key={order.id}>
                    <TableCell dir="ltr" className="text-start">{order.number ?? order.id}</TableCell>
                    <TableCell>{formatDate(order.date)}</TableCell>
                    <TableCell>{order.status}</TableCell>
                    <TableCell>{formatTotal(order)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
          <h3 className="font-medium">{t('erp.invoices')}</h3>
          {data.invoices.length === 0 ? (
            <p className="text-muted-foreground">{t('erp.noInvoices')}</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('erp.number')}</TableHead>
                  <TableHead>{t('erp.date')}</TableHead>
                  <TableHead>{t('erp.dueDate')}</TableHead>
                  <TableHead>{t('erp.status')}</TableHead>
                  <TableHead>{t('erp.total')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.invoices.map((invoice) => (
                  <TableRow key={invoice.id}>
                    <TableCell dir="ltr" className="text-start">{invoice.number ?? invoice.id}</TableCell>
                    <TableCell>{formatDate(invoice.date)}</TableCell>
                    <TableCell>{formatDate(invoice.dueDate)}</TableCell>
                    <TableCell>{invoice.status}</TableCell>
                    <TableCell>{formatTotal(invoice)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </>
      ) : null}
    </section>
  )
}

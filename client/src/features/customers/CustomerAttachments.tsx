import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useRef, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { downloadCustomerAttachment, uploadCustomerAttachment, type CustomerAttachment } from '@/api/customers'
import { isApiError } from '@/api/errors'
import { permissions } from '@/auth/permissions'
import { Button } from '@/components/ui/button'
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Can } from '@/features/auth/Can'
import { saveFile } from '@/lib/save-file'
import { checkAttachment } from './attachment-rules'
import { useCustomerAttachments } from './useCustomerNotes'
import { customersQueryKey } from './useCustomers'

/** Files of a customer: upload form (customers.manage), table with a Download button per file. */
export function CustomerAttachments({ customerId }: { customerId: string }) {
  const { t, i18n } = useTranslation()
  const attachments = useCustomerAttachments(customerId)
  const formatTime = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' })
  const formatNumber = new Intl.NumberFormat(i18n.language, { maximumFractionDigits: 1 })

  const download = useMutation({
    mutationFn: (attachment: CustomerAttachment) => downloadCustomerAttachment(customerId, attachment.id),
    onSuccess: (blob, attachment) => saveFile(blob, attachment.fileName),
  })

  function size(bytes: number) {
    return bytes < 1024 * 1024
      ? t('customers.attachments.sizeKb', { size: formatNumber.format(Math.max(bytes / 1024, 0.1)) })
      : t('customers.attachments.sizeMb', { size: formatNumber.format(bytes / (1024 * 1024)) })
  }

  return (
    <section className="flex flex-col gap-4">
      <h2 className="text-lg font-semibold text-primary">{t('customers.attachments.title')}</h2>
      <Can permission={permissions.customersManage}>
        <UploadForm customerId={customerId} />
      </Can>
      {attachments.isPending ? (
        <p className="text-muted-foreground">{t('customers.attachments.loading')}</p>
      ) : attachments.data && attachments.data.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('customers.attachments.columns.file')}</TableHead>
              <TableHead>{t('customers.attachments.columns.size')}</TableHead>
              <TableHead>{t('customers.attachments.columns.uploadedBy')}</TableHead>
              <TableHead>{t('customers.attachments.columns.uploadedAt')}</TableHead>
              <TableHead className="text-end">{t('customers.attachments.columns.actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {attachments.data.map((attachment) => (
              <TableRow key={attachment.id}>
                <TableCell dir="auto" className="text-start font-medium">
                  {attachment.fileName}
                </TableCell>
                <TableCell>{size(attachment.size)}</TableCell>
                <TableCell>{attachment.uploadedByName ?? t('customers.timeline.system')}</TableCell>
                <TableCell>
                  <time dateTime={attachment.uploadedAt}>{formatTime.format(new Date(attachment.uploadedAt))}</time>
                </TableCell>
                <TableCell>
                  <div className="flex justify-end">
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={download.isPending}
                      onClick={() => download.mutate(attachment)}
                    >
                      {t('customers.attachments.download')}
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <p className="text-muted-foreground">{t('customers.attachments.empty')}</p>
      )}
    </section>
  )
}

function UploadForm({ customerId }: { customerId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const input = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [error, setError] = useState<string | null>(null)

  const upload = useMutation({
    mutationFn: (chosen: File) => uploadCustomerAttachment(customerId, chosen),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: customersQueryKey })
      toast.success(t('customers.attachments.uploaded'))
      setFile(null)
      if (input.current) input.current.value = ''
    },
  })

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!file) {
      setError(t('customers.attachments.fileRequired'))
      return
    }
    // Same rules as the server: no upload that would be refused anyway.
    const problem = checkAttachment(file)
    if (problem) {
      setError(t(`customers.attachments.${problem}`))
      return
    }
    setError(null)
    try {
      await upload.mutateAsync(file)
    } catch (caught) {
      const message = isApiError(caught) ? caught.problem?.errors?.file?.[0] : undefined
      if (message) setError(message)
    }
  }

  return (
    <form noValidate onSubmit={onSubmit}>
      <FieldGroup>
        <Field data-invalid={error !== null}>
          <FieldLabel htmlFor="customer-attachment">{t('customers.attachments.file')}</FieldLabel>
          <Input
            ref={input}
            id="customer-attachment"
            type="file"
            aria-invalid={error !== null}
            onChange={(event) => {
              setFile(event.target.files?.[0] ?? null)
              setError(null)
            }}
          />
          <FieldDescription>{t('customers.attachments.hint')}</FieldDescription>
          {error ? <FieldError>{error}</FieldError> : null}
        </Field>
        <div>
          <Button type="submit" disabled={upload.isPending}>
            {upload.isPending ? t('customers.attachments.uploading') : t('customers.attachments.upload')}
          </Button>
        </div>
      </FieldGroup>
    </form>
  )
}

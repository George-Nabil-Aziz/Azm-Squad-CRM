import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import {
  apiKeyScopes,
  createApiKey,
  listApiKeys,
  revokeApiKey,
  type ApiKeyScope,
  type CreatedApiKey,
} from '@/api/integrations'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

const apiKeysQueryKey = ['api-keys'] as const

/** i18n key of each scope label (a colon cannot be part of a translation key path). */
const scopeLabelKey = {
  'tickets:read': 'integrations.apiKeys.scopeLabels.ticketsRead',
  'tickets:write': 'integrations.apiKeys.scopeLabels.ticketsWrite',
  'customers:read': 'integrations.apiKeys.scopeLabels.customersRead',
  'customers:write': 'integrations.apiKeys.scopeLabels.customersWrite',
} as const satisfies Record<ApiKeyScope, string>

/** API keys of the public API (CRM-58). A new key is shown once, in the banner after creating it. */
export function ApiKeysPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const keys = useQuery({ queryKey: apiKeysQueryKey, queryFn: ({ signal }) => listApiKeys(signal) })
  const [name, setName] = useState('')
  const [scopes, setScopes] = useState<ApiKeyScope[]>([])
  const [errors, setErrors] = useState<{ name?: string; scopes?: string }>({})
  const [created, setCreated] = useState<CreatedApiKey | null>(null)
  const reload = () => queryClient.invalidateQueries({ queryKey: apiKeysQueryKey })

  const create = useMutation({ mutationFn: (request: { name: string; scopes: ApiKeyScope[] }) => createApiKey(request), onSuccess: reload })
  const revoke = useMutation({ mutationFn: (id: string) => revokeApiKey(id), onSuccess: reload })

  async function submit(event: FormEvent) {
    event.preventDefault()
    const next = {
      name: name.trim() ? undefined : t('integrations.apiKeys.nameRequired'),
      scopes: scopes.length > 0 ? undefined : t('integrations.apiKeys.scopesRequired'),
    }
    setErrors(next)
    if (next.name || next.scopes) return
    try {
      setCreated(await create.mutateAsync({ name: name.trim(), scopes }))
      setName('')
      setScopes([])
    } catch {
      // The API client already shows the error as a toast.
    }
  }

  function toggle(scope: ApiKeyScope, checked: boolean) {
    setScopes((current) => (checked ? [...current, scope] : current.filter((s) => s !== scope)))
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <p className="text-muted-foreground">{t('integrations.apiKeys.description')}</p>
        <a href="/swagger" target="_blank" rel="noreferrer" className="text-sm underline">
          {t('integrations.apiKeys.swagger')}
        </a>
      </div>

      {created ? (
        <div role="status" className="flex max-w-xl flex-col gap-2 rounded-md border bg-muted p-4">
          <p className="text-sm font-medium">{t('integrations.apiKeys.created')}</p>
          <label htmlFor="created-api-key" className="text-sm">
            {t('integrations.apiKeys.keyLabel')}
          </label>
          <Input id="created-api-key" dir="ltr" readOnly value={created.key} onFocus={(e) => e.target.select()} />
          <div>
            <Button type="button" variant="outline" onClick={() => setCreated(null)}>
              {t('integrations.apiKeys.dismiss')}
            </Button>
          </div>
        </div>
      ) : null}

      <form onSubmit={(event) => void submit(event)} noValidate className="flex max-w-xl flex-col gap-3">
        <div className="flex flex-col gap-1">
          <label htmlFor="api-key-name" className="text-sm font-medium">
            {t('integrations.apiKeys.name')}
          </label>
          <Input id="api-key-name" value={name} aria-invalid={errors.name ? true : undefined} onChange={(e) => setName(e.target.value)} />
          {errors.name ? <p role="alert" className="text-sm text-destructive">{errors.name}</p> : null}
        </div>
        <fieldset className="flex flex-col gap-2">
          <legend className="text-sm font-medium">{t('integrations.apiKeys.scopes')}</legend>
          {apiKeyScopes.map((scope) => (
            <div key={scope} className="flex items-center gap-2">
              <Checkbox
                id={`scope-${scope}`}
                checked={scopes.includes(scope)}
                onCheckedChange={(checked) => toggle(scope, checked === true)}
              />
              <label htmlFor={`scope-${scope}`} className="text-sm">
                {t(scopeLabelKey[scope])}
                <span dir="ltr" className="ms-2 text-xs text-muted-foreground">
                  {scope}
                </span>
              </label>
            </div>
          ))}
          {errors.scopes ? <p role="alert" className="text-sm text-destructive">{errors.scopes}</p> : null}
        </fieldset>
        <div>
          <Button type="submit" disabled={create.isPending}>
            {t('integrations.apiKeys.create')}
          </Button>
        </div>
      </form>

      {keys.isPending ? <p className="text-muted-foreground">{t('integrations.apiKeys.loading')}</p> : null}
      {keys.data?.length === 0 ? <p className="text-muted-foreground">{t('integrations.apiKeys.empty')}</p> : null}
      {keys.data && keys.data.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('integrations.apiKeys.name')}</TableHead>
              <TableHead>{t('integrations.apiKeys.prefix')}</TableHead>
              <TableHead>{t('integrations.apiKeys.scopes')}</TableHead>
              <TableHead>{t('integrations.apiKeys.lastUsed')}</TableHead>
              <TableHead>{t('integrations.apiKeys.status')}</TableHead>
              <TableHead className="text-end">{t('integrations.apiKeys.actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {keys.data.map((key) => (
              <TableRow key={key.id}>
                <TableCell className="font-medium">{key.name}</TableCell>
                <TableCell dir="ltr" className="text-start">
                  {key.keyPrefix}…
                </TableCell>
                <TableCell>{key.scopes.map((scope) => t(scopeLabelKey[scope])).join(', ')}</TableCell>
                <TableCell>{key.lastUsedAt ? new Date(key.lastUsedAt).toLocaleString() : t('integrations.apiKeys.never')}</TableCell>
                <TableCell>
                  <Badge variant={key.revokedAt ? 'outline' : 'secondary'}>
                    {t(key.revokedAt ? 'integrations.apiKeys.revoked' : 'integrations.apiKeys.active')}
                  </Badge>
                </TableCell>
                <TableCell>
                  <div className="flex justify-end">
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      disabled={key.revokedAt !== null || revoke.isPending}
                      aria-label={t('integrations.apiKeys.revokeFor', { name: key.name })}
                      onClick={() => revoke.mutate(key.id)}
                    >
                      {t('integrations.apiKeys.revoke')}
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : null}
    </div>
  )
}

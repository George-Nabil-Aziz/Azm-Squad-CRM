import { Fragment } from 'react'
import { useTranslation } from 'react-i18next'
import { Badge } from '@/components/ui/badge'
import { splitBody } from './placeholders'

/** A reply body where each placeholder is shown as a small badge with a friendly label. */
export function QuickReplyBody({ body, className }: { body: string; className?: string }) {
  const { t } = useTranslation()

  return (
    <span dir="auto" className={className}>
      {splitBody(body).map((part, index) =>
        part.type === 'text' ? (
          <Fragment key={index}>{part.value}</Fragment>
        ) : (
          <Badge key={index} variant="secondary" className="mx-0.5 align-middle">
            {t(`quickReplies.tokens.${part.key}`)}
          </Badge>
        ),
      )}
    </span>
  )
}

/** The reply with sample values filled in, so the author sees what the customer will read. */
export function QuickReplyPreview({ body, agentName }: { body: string; agentName?: string }) {
  const { t } = useTranslation()
  const text = splitBody(body)
    .map((part) =>
      part.type === 'text' ? part.value : part.key === 'agentName' && agentName ? agentName : t(`quickReplies.samples.${part.key}`),
    )
    .join('')

  return (
    <p data-testid="quick-reply-preview" dir="auto" className="whitespace-pre-wrap rounded-lg border bg-muted/40 p-3 text-sm">
      {text}
    </p>
  )
}

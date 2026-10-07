import type * as React from 'react'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'
import { toneSoftClasses, type Tone } from '@/lib/tones'

/** A badge in one of the soft theme tones (status, priority and channel chips). */
export function ToneBadge({ tone, className, ...props }: { tone: Tone } & React.ComponentProps<'span'>) {
  return <Badge variant="tone" data-tone={tone} className={cn(toneSoftClasses[tone], className)} {...props} />
}

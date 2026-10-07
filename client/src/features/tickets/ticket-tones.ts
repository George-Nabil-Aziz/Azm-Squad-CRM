import type { Tone } from '@/lib/tones'
import type { TicketChannel, TicketPriority, TicketStatus } from './ticket-values'

/** One soft colour per workflow status. */
export const statusTones: Record<TicketStatus, Tone> = {
  new: 'sky',
  open: 'indigo',
  pending: 'amber',
  resolved: 'emerald',
  closed: 'slate',
}

/** One soft colour per priority (the high priority reads as urgent). */
export const priorityTones: Record<TicketPriority, Tone> = {
  low: 'slate',
  mid: 'amber',
  high: 'rose',
}

/** One soft colour per channel. */
export const channelTones: Record<TicketChannel, Tone> = {
  manual: 'slate',
  email: 'sky',
  whatsapp: 'emerald',
  chat: 'violet',
  sms: 'orange',
  webform: 'teal',
  portal: 'indigo',
}

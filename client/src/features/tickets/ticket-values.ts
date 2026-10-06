/** Fixed ticket priorities, highest first (server: TicketValues.PriorityNames). Labels: `tickets.priorities.<name>`. */
export const ticketPriorities = ['high', 'mid', 'low'] as const

export type TicketPriority = (typeof ticketPriorities)[number]

/** Workflow steps in order (server: TicketValues.StatusNames). Labels: `tickets.statuses.<name>`. */
export const ticketStatuses = ['new', 'open', 'pending', 'resolved', 'closed'] as const

export type TicketStatus = (typeof ticketStatuses)[number]

/** How a ticket came in (server: TicketChannel). Labels: `tickets.channels.<name>`. */
export const ticketChannels = ['manual', 'email', 'whatsapp', 'portal'] as const

export type TicketChannel = (typeof ticketChannels)[number]

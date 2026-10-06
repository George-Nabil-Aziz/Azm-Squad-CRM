/** Fixed ticket priorities, highest first (server: TicketValues.PriorityNames). Labels: `tickets.priorities.<name>`. */
export const ticketPriorities = ['high', 'mid', 'low'] as const

export type TicketPriority = (typeof ticketPriorities)[number]

/** The placeholders the server fills in (CRM-32). The stored format stays `{{token}}`. */
export const quickReplyTokens = [
  { token: '{{customer.name}}', key: 'customerName' },
  { token: '{{ticket.number}}', key: 'ticketNumber' },
  { token: '{{ticket.subject}}', key: 'ticketSubject' },
  { token: '{{agent.name}}', key: 'agentName' },
] as const

export type QuickReplyTokenKey = (typeof quickReplyTokens)[number]['key']

const tokenPattern = /\{\{\s*(customer\.name|ticket\.number|ticket\.subject|agent\.name)\s*\}\}/g
const keyByName: Record<string, QuickReplyTokenKey> = {
  'customer.name': 'customerName',
  'ticket.number': 'ticketNumber',
  'ticket.subject': 'ticketSubject',
  'agent.name': 'agentName',
}

export type BodyPart = { type: 'text'; value: string } | { type: 'token'; key: QuickReplyTokenKey }

/** Splits a reply body into plain text and placeholder parts. */
export function splitBody(body: string): BodyPart[] {
  const parts: BodyPart[] = []
  let last = 0
  for (const match of body.matchAll(tokenPattern)) {
    if (match.index > last) parts.push({ type: 'text', value: body.slice(last, match.index) })
    parts.push({ type: 'token', key: keyByName[match[1]] })
    last = match.index + match[0].length
  }
  if (last < body.length) parts.push({ type: 'text', value: body.slice(last) })
  return parts
}

/** Inserts `token` into `text` replacing the selection; returns the new text and the caret position after the token. */
export function insertAt(text: string, token: string, start: number, end: number) {
  return { text: text.slice(0, start) + token + text.slice(end), caret: start + token.length }
}

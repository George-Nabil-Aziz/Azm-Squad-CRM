/** Same limits as the server (Crm.Application.Customers.Attachments.AttachmentRules): checked before uploading. */
export const MAX_ATTACHMENT_BYTES = 10 * 1024 * 1024

export const ALLOWED_ATTACHMENT_EXTENSIONS = [
  '.pdf',
  '.png',
  '.jpg',
  '.jpeg',
  '.gif',
  '.webp',
  '.txt',
  '.csv',
  '.doc',
  '.docx',
  '.xls',
  '.xlsx',
] as const

export type AttachmentProblem = 'tooLarge' | 'typeNotAllowed' | 'empty'

/** Why the file cannot be uploaded, or null when it may. */
export function checkAttachment(file: File): AttachmentProblem | null {
  const dot = file.name.lastIndexOf('.')
  const extension = dot >= 0 ? file.name.slice(dot).toLowerCase() : ''
  if (!(ALLOWED_ATTACHMENT_EXTENSIONS as readonly string[]).includes(extension)) return 'typeNotAllowed'
  if (file.size > MAX_ATTACHMENT_BYTES) return 'tooLarge'
  if (file.size === 0) return 'empty'
  return null
}

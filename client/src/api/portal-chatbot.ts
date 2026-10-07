import { apiGet, apiPost } from './client'

/** GET /api/portal/chatbot/status: false while the server has no AI key (the portal then hides the chat). */
export interface ChatbotStatus {
  enabled: boolean
}

export function getChatbotStatus(signal?: AbortSignal): Promise<ChatbotStatus> {
  return apiGet<ChatbotStatus>('/api/portal/chatbot/status', signal)
}

/** One message of the transcript the page keeps and sends (the server is stateless). */
export interface ChatbotMessage {
  role: 'user' | 'assistant'
  content: string
}

/** A published article an answer cites. */
export interface ChatbotSource {
  id: string
  title: string
}

/** The chatbot reply (server: ChatbotReply). `outcome`: answered | unknown | handoff. */
export interface ChatbotReply {
  answer: string
  language: 'ar' | 'en'
  sources: ChatbotSource[]
  outcome: 'answered' | 'unknown' | 'handoff'
  offerAgent: boolean
  signInRequired: boolean
  ticket: { id: string; number: string } | null
}

/** Sends the transcript so far (the last message is the customer message); `handoff` asks for a human. */
export function sendChatbotMessage(messages: ChatbotMessage[], handoff: boolean): Promise<ChatbotReply> {
  return apiPost<ChatbotReply>('/api/portal/chatbot/messages', { messages, handoff })
}

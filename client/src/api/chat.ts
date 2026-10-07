import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { getAccessToken } from '../auth/session'
import { apiGet, apiPost } from './client'
import type { WebFormInput, WebFormReceipt } from './web-forms'

/** A chat (server: ChatSessionResponse). `status`: waiting | active | ended. */
export interface ChatSession {
  id: string
  visitorName: string
  visitorEmail: string
  status: 'waiting' | 'active' | 'ended'
  agentId: string | null
  agentName: string | null
  startedAt: string
  endedAt: string | null
  ticketNumber: string | null
}

/** A chat message (server: ChatMessageResponse). `sender`: visitor | agent. */
export interface ChatMessage {
  id: string
  sessionId: string
  sender: 'visitor' | 'agent'
  senderName: string
  body: string
  sentAt: string
}

export interface StartChatInput {
  name: string
  email: string
  message: string
}

/** The new chat and the secret the visitor connects with (shown once). */
export interface StartChatResponse {
  session: ChatSession
  visitorToken: string
}

export function getChatAvailability(signal?: AbortSignal): Promise<{ available: boolean }> {
  return apiGet<{ available: boolean }>('/api/public/chat/availability', signal)
}

/** 201; 409 when no agent is online (use the offline form); 400 on the fields; 429 when sent too often. */
export function startChat(input: StartChatInput): Promise<StartChatResponse> {
  return apiPost<StartChatResponse>('/api/public/chat/sessions', input)
}

/** The offline form: same fields as the web form, the ticket gets the channel "chat". */
export function submitOfflineChat(input: WebFormInput): Promise<WebFormReceipt> {
  return apiPost<WebFormReceipt>('/api/public/chat/offline', input)
}

/** Agents: the queue ("waiting") or their own chats ("active"). */
export function listChatSessions(status: 'waiting' | 'active', signal?: AbortSignal): Promise<ChatSession[]> {
  return apiGet<ChatSession[]>(`/api/chat-sessions?status=${status}`, signal)
}

export function listChatMessages(sessionId: string, signal?: AbortSignal): Promise<ChatMessage[]> {
  return apiGet<ChatMessage[]>(`/api/chat-sessions/${sessionId}/messages`, signal)
}

/** Events the server pushes (server: ChatHub). */
export interface ChatHandlers {
  onChatStarted?: (session: ChatSession) => void
  onChatAccepted?: (session: ChatSession) => void
  onMessage?: (message: ChatMessage) => void
  onChatEnded?: (session: ChatSession) => void
}

/** An open chat hub connection. Hub methods (names are case-insensitive): accept (agents), send, end. */
export interface ChatConnection {
  accept: (sessionId: string) => Promise<ChatSession>
  send: (sessionId: string, body: string) => Promise<ChatMessage>
  end: (sessionId: string) => Promise<ChatSession>
  stop: () => Promise<void>
}

interface ConnectOptions {
  /** Visitors: the chat id and the secret from `startChat`. Agents: leave out (the staff token is used). */
  visitor?: { sessionId: string; token: string }
  handlers: ChatHandlers
}

/**
 * Opens the chat connection (SignalR, `/hubs/chat`), reconnecting automatically.
 * Tests replace this function: no network is opened in tests.
 */
export async function connectChatHub({ visitor, handlers }: ConnectOptions): Promise<ChatConnection> {
  const url = visitor
    ? `/hubs/chat?session=${encodeURIComponent(visitor.sessionId)}&token=${encodeURIComponent(visitor.token)}`
    : '/hubs/chat'
  const connection = new HubConnectionBuilder()
    .withUrl(url, visitor ? {} : { accessTokenFactory: () => getAccessToken() ?? '' })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.None)
    .build()
  connection.on('ChatStarted', (session: ChatSession) => handlers.onChatStarted?.(session))
  connection.on('ChatAccepted', (session: ChatSession) => handlers.onChatAccepted?.(session))
  connection.on('MessageReceived', (message: ChatMessage) => handlers.onMessage?.(message))
  connection.on('ChatEnded', (session: ChatSession) => handlers.onChatEnded?.(session))
  await connection.start()
  return {
    accept: (sessionId) => connection.invoke<ChatSession>('accept', sessionId),
    send: (sessionId, body) => connection.invoke<ChatMessage>('send', sessionId, body),
    end: (sessionId) => connection.invoke<ChatSession>('end', sessionId),
    stop: () => connection.stop(),
  }
}

/** Writes a message into the open chat. */
export type ChatSend = (body: string) => Promise<unknown>

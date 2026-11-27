import { useInfiniteQuery, useQuery, type InfiniteData, type QueryClient } from '@tanstack/react-query'
import { conversationsApi, filesApi, friendsApi, messagesApi } from '../api/endpoints'
import { PAGE_SIZE } from '../constants'
import type {
  ConversationDto, FriendDto, FriendRequestsDto, MessageDto,
} from '../types/api'
import { sortConversations } from '../utils/format'

export const keys = {
  conversations: ['conversations'] as const,
  conversation: (id: string) => ['conversation', id] as const,
  messages: (id: string) => ['messages', id] as const,
  friends: ['friends'] as const,
  requests: ['requests'] as const,
  stickers: ['stickers'] as const,
}

export type MessagePages = InfiniteData<MessageDto[], number | undefined>

export const useConversations = () =>
  useQuery({ queryKey: keys.conversations, queryFn: conversationsApi.list, select: sortConversations })

export const useConversation = (id: string) =>
  useQuery({ queryKey: keys.conversation(id), queryFn: () => conversationsApi.get(id) })

export const useFriends = () => useQuery({ queryKey: keys.friends, queryFn: friendsApi.list })
export const useRequests = () => useQuery({ queryKey: keys.requests, queryFn: friendsApi.requests })
export const useStickers = () =>
  useQuery({ queryKey: keys.stickers, queryFn: filesApi.stickers, staleTime: Infinity })

export const useMessages = (convId: string) =>
  useInfiniteQuery({
    queryKey: keys.messages(convId),
    initialPageParam: undefined as number | undefined,
    queryFn: ({ pageParam }) => messagesApi.history(convId, pageParam),
    // Pages are newest -> oldest; the cursor is the oldest id of the last loaded page.
    getNextPageParam: (last) => (last.length < PAGE_SIZE ? undefined : last[last.length - 1].id),
  })

// ---------- cache mutators (used by mutations and SignalR handlers) ----------

/** Insert a message into the cached history (deduped by id). Returns false when the history is not cached. */
export function addMessageToCache(qc: QueryClient, msg: MessageDto): boolean {
  let applied = false
  qc.setQueryData<MessagePages>(keys.messages(msg.conversationId), (old) => {
    if (!old) return old
    applied = true
    if (old.pages.some((p) => p.some((m) => m.id === msg.id))) return old
    const [first, ...rest] = old.pages
    return { ...old, pages: [[msg, ...(first ?? [])], ...rest] }
  })
  return applied
}

export function patchMessage(qc: QueryClient, convId: string, messageId: number, patch: Partial<MessageDto>) {
  qc.setQueryData<MessagePages>(keys.messages(convId), (old) =>
    old && { ...old, pages: old.pages.map((p) => p.map((m) => (m.id === messageId ? { ...m, ...patch } : m))) })
}

export function upsertConversation(qc: QueryClient, conv: ConversationDto) {
  qc.setQueryData<ConversationDto[]>(keys.conversations, (old) => {
    if (!old) return old
    return old.some((c) => c.id === conv.id) ? old.map((c) => (c.id === conv.id ? conv : c)) : [conv, ...old]
  })
  qc.setQueryData(keys.conversation(conv.id), conv)
}

export function patchConversation(qc: QueryClient, id: string, fn: (c: ConversationDto) => ConversationDto) {
  qc.setQueryData<ConversationDto[]>(keys.conversations, (old) => old?.map((c) => (c.id === id ? fn(c) : c)))
  qc.setQueryData<ConversationDto>(keys.conversation(id), (old) => old && fn(old))
}

export function setFriends(qc: QueryClient, fn: (f: FriendDto[]) => FriendDto[]) {
  qc.setQueryData<FriendDto[]>(keys.friends, (old) => old && fn(old))
}

export function setRequests(qc: QueryClient, fn: (r: FriendRequestsDto) => FriendRequestsDto) {
  qc.setQueryData<FriendRequestsDto>(keys.requests, (old) => old && fn(old))
}

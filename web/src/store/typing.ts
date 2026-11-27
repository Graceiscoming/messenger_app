import { useSyncExternalStore } from 'react'
import { TYPING_EXPIRE_MS } from '../constants'

// Tiny external store: conversationId -> userIds currently typing (expires after TYPING_EXPIRE_MS).
let snapshot: Record<string, string[]> = {}
const timers = new Map<string, ReturnType<typeof setTimeout>>()
const listeners = new Set<() => void>()
const emit = () => listeners.forEach((l) => l())

function remove(conv: string, user: string) {
  const list = snapshot[conv]
  if (!list?.includes(user)) return
  snapshot = { ...snapshot, [conv]: list.filter((u) => u !== user) }
  emit()
}

export function setTyping(conv: string, user: string) {
  const key = `${conv}|${user}`
  clearTimeout(timers.get(key))
  timers.set(key, setTimeout(() => { timers.delete(key); remove(conv, user) }, TYPING_EXPIRE_MS))
  if (!snapshot[conv]?.includes(user)) {
    snapshot = { ...snapshot, [conv]: [...(snapshot[conv] ?? []), user] }
    emit()
  }
}

export function clearTyping(conv: string, user: string) {
  const key = `${conv}|${user}`
  clearTimeout(timers.get(key))
  timers.delete(key)
  remove(conv, user)
}

const subscribe = (l: () => void) => { listeners.add(l); return () => { listeners.delete(l) } }
const EMPTY: string[] = []

export function useTypingUsers(conv: string | undefined): string[] {
  return useSyncExternalStore(subscribe, () => (conv ? snapshot[conv] ?? EMPTY : EMPTY))
}

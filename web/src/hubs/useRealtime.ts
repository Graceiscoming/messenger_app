import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { createHub, setCurrentHub, startWithRetry } from './chatHub'
import { normalizeConversation } from '../api/normalize'
import {
  addMessageToCache, keys, patchConversation, patchMessage, setFriends, setRequests, upsertConversation,
} from '../hooks/queries'
import { activeConversation } from '../store/activeConversation'
import { clearTyping, setTyping } from '../store/typing'
import type {
  ConversationDto, FriendRemovedEvent, FriendshipDto, MessageDto, MessageReadEvent, MessageUnsentEvent,
  PresenceChangedEvent, UserTypingEvent,
} from '../types/api'

/**
 * Connects the SignalR hub for the logged-in user and applies each server event to the React Query
 * cache surgically (no "refetch everything" on events). A full refresh only happens after a reconnect,
 * to recover events missed while offline.
 */
export function useRealtime(meId: string) {
  const qc = useQueryClient()
  const navigate = useNavigate()

  useEffect(() => {
    const hub = createHub()
    let cancelled = false

    hub.on('MessageReceived', (msg: MessageDto) => {
      clearTyping(msg.conversationId, msg.sender.id)
      addMessageToCache(qc, msg)
      const seen = activeConversation.id === msg.conversationId && document.visibilityState === 'visible'
      const known = qc.getQueryData<ConversationDto[]>(keys.conversations)?.some((c) => c.id === msg.conversationId)
      if (known === false) {
        void qc.invalidateQueries({ queryKey: keys.conversations }) // unknown room: fetch just the list once
        return
      }
      patchConversation(qc, msg.conversationId, (c) => ({
        ...c,
        lastMessage: msg,
        unreadCount: msg.sender.id === meId || seen ? c.unreadCount : c.unreadCount + 1,
      }))
    })

    hub.on('MessageUnsent', ({ conversationId, messageId }: MessageUnsentEvent) => {
      patchMessage(qc, conversationId, messageId, { isUnsent: true, content: null, attachmentUrl: null, sticker: null })
      patchConversation(qc, conversationId, (c) =>
        c.lastMessage?.id === messageId
          ? { ...c, lastMessage: { ...c.lastMessage, isUnsent: true, content: null, attachmentUrl: null, sticker: null } }
          : c)
    })

    hub.on('MessageRead', ({ conversationId, userId, messageId }: MessageReadEvent) => {
      patchConversation(qc, conversationId, (c) => ({
        ...c,
        unreadCount: userId === meId ? 0 : c.unreadCount,
        members: c.members.map((m) =>
          m.user.id === userId && (m.lastReadMessageId ?? 0) < messageId ? { ...m, lastReadMessageId: messageId } : m),
      }))
    })

    hub.on('ConversationChanged', (raw: ConversationDto) => {
      const conv = normalizeConversation(raw)
      if (!conv.members.some((m) => m.user.id === meId)) {
        // we were removed from the room
        qc.setQueryData<ConversationDto[]>(keys.conversations, (old) => old?.filter((c) => c.id !== conv.id))
        if (activeConversation.id === conv.id) navigate('/', { replace: true })
        return
      }
      upsertConversation(qc, conv)
    })

    hub.on('FriendRequestReceived', (f: FriendshipDto) => {
      setRequests(qc, (r) =>
        r.incoming.some((x) => x.id === f.id) ? r : { ...r, incoming: [f, ...r.incoming] })
    })

    hub.on('FriendRequestAccepted', (f: FriendshipDto) => {
      setRequests(qc, (r) => ({
        incoming: r.incoming.filter((x) => x.id !== f.id),
        outgoing: r.outgoing.filter((x) => x.id !== f.id),
      }))
      // The friend list needs conversationId/isOnline from the server: one targeted refetch.
      void qc.invalidateQueries({ queryKey: keys.friends })
    })

    hub.on('FriendRemoved', ({ userId }: FriendRemovedEvent) => {
      setFriends(qc, (list) => list.filter((f) => f.user.id !== userId))
    })

    hub.on('PresenceChanged', ({ userId, isOnline }: PresenceChangedEvent) => {
      setFriends(qc, (list) => list.map((f) => (f.user.id === userId ? { ...f, isOnline } : f)))
    })

    hub.on('UserTyping', ({ conversationId, userId }: UserTypingEvent) => {
      if (userId !== meId) setTyping(conversationId, userId)
    })

    hub.onreconnected(() => {
      void qc.invalidateQueries({ queryKey: keys.conversations })
      void qc.invalidateQueries({ queryKey: keys.friends })
      void qc.invalidateQueries({ queryKey: keys.requests })
      void qc.invalidateQueries({ queryKey: ['messages'] })
    })

    setCurrentHub(hub)
    void startWithRetry(hub, () => cancelled)

    return () => {
      cancelled = true
      setCurrentHub(null)
      void hub.stop()
    }
  }, [qc, meId, navigate])
}

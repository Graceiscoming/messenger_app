import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { errorMessage } from '../api/client'
import { conversationsApi, messagesApi } from '../api/endpoints'
import Avatar from '../components/Avatar'
import Composer from '../components/Composer'
import GroupInfoDialog from '../components/GroupInfoDialog'
import { BackIcon, ChatIcon, InfoIcon } from '../components/icons'
import MessageBubble from '../components/MessageBubble'
import { EmptyState, MessagesSkeleton } from '../components/Skeletons'
import { patchConversation, patchMessage, useConversation, useFriends, useMessages } from '../hooks/queries'
import { useT } from '../i18n'
import { activeConversation } from '../store/activeConversation'
import { useMe } from '../store/authContext'
import { useTypingUsers } from '../store/typing'
import type { MessageDto } from '../types/api'
import { convAvatar, convTitle, formatDay, otherMember, parseDate, sameDay } from '../utils/format'

const GROUP_GAP_MS = 5 * 60_000

export default function ChatRoom() {
  const { id = '' } = useParams()
  return <ChatRoomInner key={id} id={id} />
}

function ChatRoomInner({ id }: { id: string }) {
  const { t } = useT()
  const me = useMe()
  const qc = useQueryClient()
  const navigate = useNavigate()
  const conv = useConversation(id)
  const { data: friends } = useFriends()
  const msgs = useMessages(id)
  const typingIds = useTypingUsers(id)

  const [visible, setVisible] = useState(document.visibilityState === 'visible')
  const [menuFor, setMenuFor] = useState<number | null>(null)
  const [reply, setReply] = useState<MessageDto | null>(null)
  const [forbidden, setForbidden] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [showInfo, setShowInfo] = useState(false)
  const [lightbox, setLightbox] = useState<string | null>(null)
  // Messages with an id above this arrived after the room finished loading -> animate them in.
  const [baseline, setBaseline] = useState<number | null>(null)

  const scrollRef = useRef<HTMLDivElement>(null)
  const stick = useRef(true)
  const anchor = useRef<{ height: number; top: number } | null>(null)
  const lastMarked = useRef(0)

  // newest -> oldest pages flattened, then reversed for display
  const messages = useMemo(() => (msgs.data?.pages.flat() ?? []).slice().reverse(), [msgs.data])
  const newest = messages[messages.length - 1]
  if (baseline === null && msgs.isSuccess) setBaseline(newest?.id ?? 0)

  useEffect(() => {
    activeConversation.set(id)
    return () => activeConversation.set(null)
  }, [id])

  useEffect(() => {
    const on = () => setVisible(document.visibilityState === 'visible')
    document.addEventListener('visibilitychange', on)
    return () => document.removeEventListener('visibilitychange', on)
  }, [])

  // Mark read when the newest message is on screen.
  const unread = conv.data?.unreadCount ?? 0
  useEffect(() => {
    if (!newest || !visible || newest.id <= lastMarked.current) return
    if (newest.sender.id === me.id && unread === 0) { lastMarked.current = newest.id; return }
    lastMarked.current = newest.id
    patchConversation(qc, id, (c) => ({
      ...c,
      unreadCount: 0,
      members: c.members.map((m) => (m.user.id === me.id ? { ...m, lastReadMessageId: newest.id } : m)),
    }))
    conversationsApi.markRead(id, newest.id).catch(() => { lastMarked.current = 0 })
  }, [newest, visible, unread, id, me.id, qc])

  const scrollBottom = useCallback(() => {
    const el = scrollRef.current
    if (el) el.scrollTop = el.scrollHeight
  }, [])

  useLayoutEffect(() => {
    const el = scrollRef.current
    if (!el) return
    if (anchor.current) {
      el.scrollTop = el.scrollHeight - anchor.current.height + anchor.current.top
      anchor.current = null
    } else if (stick.current || newest?.sender.id === me.id) {
      scrollBottom()
    }
  }, [messages.length, newest, me.id, scrollBottom])

  const onScroll = () => {
    const el = scrollRef.current
    if (!el) return
    stick.current = el.scrollHeight - el.scrollTop - el.clientHeight < 80
    if (el.scrollTop < 60 && msgs.hasNextPage && !msgs.isFetchingNextPage) {
      anchor.current = { height: el.scrollHeight, top: el.scrollTop }
      void msgs.fetchNextPage()
    }
  }

  const unsend = async (m: MessageDto) => {
    setMenuFor(null)
    if (!window.confirm(t('msg.confirmUnsend'))) return
    setError(null)
    try {
      await messagesApi.unsend(m.id)
      const patch = { isUnsent: true, content: null, attachmentUrl: null, sticker: null }
      patchMessage(qc, id, m.id, patch)
      patchConversation(qc, id, (c) => (c.lastMessage?.id === m.id ? { ...c, lastMessage: { ...c.lastMessage, ...patch } } : c))
    } catch (err) {
      setError(errorMessage(err))
    }
  }

  if (conv.isError) {
    return (
      <div className="chat-empty">
        <EmptyState icon={<ChatIcon />} title={t('chat.notFound')} action={<button className="btn" onClick={() => navigate('/')}>{t('common.back')}</button>} />
      </div>
    )
  }
  const c = conv.data
  const other = c?.type === 'Direct' ? otherMember(c, me.id) : null
  const friend = other ? friends?.find((f) => f.user.id === other.id) : undefined
  const readOnlyDirect = !!other && !!friends && !friend
  const readOnly = c?.isReadOnly === true || readOnlyDirect || forbidden !== null
  const title = c ? convTitle(c, me.id) : ''
  const typingNames = typingIds
    .map((u) => c?.members.find((m) => m.user.id === u)?.user.displayName)
    .filter(Boolean) as string[]

  const readLabel = (m: MessageDto): string | null => {
    if (!c || m.sender.id !== me.id || m.isUnsent) return null
    const others = c.members.filter((x) => x.user.id !== me.id)
    const n = others.filter((x) => (x.lastReadMessageId ?? 0) >= m.id).length
    if (n === 0) return null
    return c.type === 'Direct' ? t('msg.read') : t('msg.readN', { n })
  }

  const grouped = (a: MessageDto | undefined, b: MessageDto) =>
    !!a && a.type !== 'System' && b.type !== 'System' && a.sender.id === b.sender.id && sameDay(a.createdAt, b.createdAt)
    && parseDate(b.createdAt).getTime() - parseDate(a.createdAt).getTime() < GROUP_GAP_MS

  return (
    <div className="chat">
      <header className="chat-header">
        <button className="icon-btn back" onClick={() => navigate('/')} aria-label={t('common.back')}><BackIcon /></button>
        {c && <Avatar url={convAvatar(c, me.id)} name={title} group={c.type === 'Group' && !c.avatarUrl} online={friend?.isOnline} />}
        <div className="chat-title">
          <strong>{title}</strong>
          <small className={typingNames.length > 0 || friend?.isOnline ? 'accent' : ''}>
            {typingNames.length > 0 ? t('chat.typing')
              : c?.type === 'Group' ? t('chat.members', { n: c.members.length })
              : friend ? (friend.isOnline ? t('common.online') : t('common.offline')) : ''}
          </small>
        </div>
        {c?.type === 'Group' && (
          <button className="icon-btn" onClick={() => setShowInfo(true)} aria-label={t('chat.groupInfo')} title={t('chat.groupInfo')}><InfoIcon /></button>
        )}
      </header>

      <div className="messages" ref={scrollRef} onScroll={onScroll} onClick={() => setMenuFor(null)}>
        {msgs.isFetchingNextPage && <div className="loading-older">{t('chat.loadingOlder')}</div>}
        {msgs.isLoading && <MessagesSkeleton />}
        {msgs.isError && <div className="empty error">{t('chat.loadError')} <button className="link" onClick={() => void msgs.refetch()}>{t('common.retry')}</button></div>}
        {msgs.isSuccess && messages.length === 0 && (
          <EmptyState icon={<ChatIcon />} title={t('chat.empty')} hint={t('chat.emptyHint')} />
        )}
        {messages.map((m, i) => {
          const prev = messages[i - 1]
          const next = messages[i + 1]
          return (
            <div key={m.id}>
              {(i === 0 || !sameDay(prev.createdAt, m.createdAt)) && (
                <div className="day-sep"><span>{formatDay(m.createdAt)}</span></div>
              )}
              <MessageBubble
                msg={m}
                mine={m.sender.id === me.id}
                isGroup={c?.type === 'Group'}
                groupedPrev={grouped(prev, m)}
                groupedNext={!!next && grouped(m, next)}
                animate={baseline !== null && m.id > baseline}
                readLabel={readLabel(m)}
                menuOpen={menuFor === m.id}
                onToggleMenu={() => setMenuFor(menuFor === m.id ? null : m.id)}
                onReply={() => { setReply(m); setMenuFor(null) }}
                onUnsend={() => void unsend(m)}
                onImageClick={setLightbox}
                onMediaLoad={() => { if (stick.current) scrollBottom() }}
              />
            </div>
          )
        })}
        {typingNames.length > 0 && (
          <div className="typing" aria-live="polite">
            <span className="typing-dots"><i /><i /><i /></span>
            <span>{t('chat.typingNames', { names: typingNames.join(', ') })}</span>
          </div>
        )}
      </div>

      {error && <p className="error pad" role="alert">{error}</p>}
      {readOnly && <div className="readonly-note">{forbidden ?? t('chat.readonly')}</div>}
      <Composer
        conversationId={id}
        disabled={readOnly || !c}
        reply={reply}
        onClearReply={() => setReply(null)}
        onForbidden={setForbidden}
        onSent={() => { stick.current = true }}
      />

      {showInfo && <GroupInfoDialog conversationId={id} onClose={() => setShowInfo(false)} />}
      {lightbox && (
        <div className="lightbox" onClick={() => setLightbox(null)}>
          <img src={lightbox} alt={t('msg.imageAlt')} />
        </div>
      )}
    </div>
  )
}

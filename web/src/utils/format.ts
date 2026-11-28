import type { ConversationDto, MessageDto, UserDto } from '../types/api'
import { localeOf, translate } from '../i18n'
import { getSettings } from '../store/settings'

/** Server timestamps are UTC; add 'Z' if the serializer omitted the offset. */
export function parseDate(s: string): Date {
  return new Date(/([zZ]|[+-]\d\d:?\d\d)$/.test(s) ? s : `${s}Z`)
}

const loc = () => localeOf(getSettings().language)
const fmt = (o: Intl.DateTimeFormatOptions) => new Intl.DateTimeFormat(loc(), o)

export const formatTime = (s: string) => fmt({ hour: '2-digit', minute: '2-digit' }).format(parseDate(s))

const startOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime()
const dayDiff = (d: Date) => Math.round((startOfDay(new Date()) - startOfDay(d)) / 86_400_000)

/** Day separator label: today / yesterday / full date, following the UI language. */
export function formatDay(s: string): string {
  const d = parseDate(s)
  const diff = dayDiff(d)
  if (diff === 0) return translate('time.today')
  if (diff === 1) return translate('time.yesterday')
  return fmt({ weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }).format(d)
}

/** Conversation-list timestamp: time today, "yesterday", otherwise a short date. */
export function formatListTime(s: string): string {
  const d = parseDate(s)
  const diff = dayDiff(d)
  if (diff === 0) return fmt({ hour: '2-digit', minute: '2-digit' }).format(d)
  if (diff === 1) return translate('time.yesterday')
  return fmt({ day: 'numeric', month: 'short' }).format(d)
}

export const sameDay = (a: string, b: string) => parseDate(a).toDateString() === parseDate(b).toDateString()

export function otherMember(conv: ConversationDto, meId: string): UserDto | null {
  return conv.members.find((m) => m.user.id !== meId)?.user ?? null
}

export function convTitle(conv: ConversationDto, meId: string): string {
  if (conv.type === 'Group') return conv.name || translate('group.default')
  return otherMember(conv, meId)?.displayName ?? translate('title.chat')
}

export function convAvatar(conv: ConversationDto, meId: string): string | null {
  return conv.type === 'Group' ? conv.avatarUrl : otherMember(conv, meId)?.avatarUrl ?? conv.avatarUrl
}

export function messagePreview(m: MessageDto | null, meId: string): string {
  if (!m) return translate('preview.none')
  if (m.isUnsent) return translate(m.sender.id === meId ? 'preview.unsentMine' : 'preview.unsentOther')
  switch (m.type) {
    case 'Image': return m.content ? translate('preview.imageWith', { text: m.content }) : translate('preview.image')
    case 'Sticker': return translate('preview.sticker')
    default: return m.content ?? ''
  }
}

export function sortConversations(list: ConversationDto[]): ConversationDto[] {
  return [...list].sort((a, b) => (b.lastMessage?.createdAt ?? '').localeCompare(a.lastMessage?.createdAt ?? ''))
}

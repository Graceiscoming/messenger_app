import type { ConversationDto, MessageDto, UserDto } from '../types/api'

/** Server timestamps are UTC; add 'Z' if the serializer omitted the offset. */
export function parseDate(s: string): Date {
  return new Date(/([zZ]|[+-]\d\d:?\d\d)$/.test(s) ? s : `${s}Z`)
}

const timeFmt = new Intl.DateTimeFormat('th-TH', { hour: '2-digit', minute: '2-digit' })
const dayFmt = new Intl.DateTimeFormat('th-TH', { day: 'numeric', month: 'short' })
const dayLongFmt = new Intl.DateTimeFormat('th-TH', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })

export const formatTime = (s: string) => timeFmt.format(parseDate(s))
export const formatDay = (s: string) => dayLongFmt.format(parseDate(s))

export function formatListTime(s: string): string {
  const d = parseDate(s)
  const now = new Date()
  return d.toDateString() === now.toDateString() ? timeFmt.format(d) : dayFmt.format(d)
}

export const sameDay = (a: string, b: string) => parseDate(a).toDateString() === parseDate(b).toDateString()

export function otherMember(conv: ConversationDto, meId: string): UserDto | null {
  return conv.members.find((m) => m.user.id !== meId)?.user ?? null
}

export function convTitle(conv: ConversationDto, meId: string): string {
  if (conv.type === 'Group') return conv.name || 'กลุ่ม'
  return otherMember(conv, meId)?.displayName ?? 'แชท'
}

export function convAvatar(conv: ConversationDto, meId: string): string | null {
  return conv.type === 'Group' ? conv.avatarUrl : otherMember(conv, meId)?.avatarUrl ?? conv.avatarUrl
}

export function messagePreview(m: MessageDto | null, meId: string): string {
  if (!m) return 'ยังไม่มีข้อความ'
  if (m.isUnsent) return m.sender.id === meId ? 'คุณยกเลิกการส่งข้อความ' : 'ข้อความนี้ถูกยกเลิกการส่ง'
  switch (m.type) {
    case 'Image': return m.content ? `[รูปภาพ] ${m.content}` : '[รูปภาพ]'
    case 'Sticker': return '[สติกเกอร์]'
    default: return m.content ?? ''
  }
}

export function sortConversations(list: ConversationDto[]): ConversationDto[] {
  return [...list].sort((a, b) => (b.lastMessage?.createdAt ?? '').localeCompare(a.lastMessage?.createdAt ?? ''))
}

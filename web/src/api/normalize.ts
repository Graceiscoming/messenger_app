import type { ConversationDto } from '../types/api'

/** The DTO shape is fixed by the Domain layer; only defend against missing optional fields. */
export function normalizeConversation(raw: ConversationDto): ConversationDto {
  return {
    ...raw,
    members: raw.members ?? [],
    unreadCount: raw.unreadCount ?? 0,
    lastMessage: raw.lastMessage ?? null,
    isReadOnly: raw.isReadOnly ?? false,
  }
}

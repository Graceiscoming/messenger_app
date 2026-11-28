// Mirrors the DTOs in docs/04-class-diagram.md §4 and docs/06-api-spec.md (camelCase, enums as strings).

export type MessageType = 'Text' | 'Image' | 'Sticker' | 'System'
export type ConversationType = 'Direct' | 'Group'
export type MemberRole = 'Member' | 'Admin' | 'Owner'
export type FriendshipStatus = 'Pending' | 'Accepted'

export interface UserDto {
  id: string
  username: string
  displayName: string
  statusMessage: string | null
  avatarUrl: string | null
}

export interface AuthResultDto {
  accessToken: string
  refreshToken: string
  expiresIn: number
  user: UserDto
}

export interface FriendDto {
  user: UserDto
  isOnline: boolean
  conversationId: string | null
}

export interface FriendshipDto {
  id: string
  requester: UserDto
  addressee: UserDto
  status: FriendshipStatus
}

export interface FriendRequestsDto {
  incoming: FriendshipDto[]
  outgoing: FriendshipDto[]
}

export interface ConversationMemberDto {
  user: UserDto
  role: MemberRole
  lastReadMessageId: number | null
}

export interface StickerDto {
  id: number
  code: string
  imageUrl: string
}

export interface StickerPackDto {
  id: number
  name: string
  thumbnailUrl: string | null
  stickers: StickerDto[]
}

export interface ReplyDto {
  id: number
  senderId: string
  senderDisplayName: string
  type: MessageType
  content: string | null
  isUnsent: boolean
}

export interface MessageDto {
  id: number
  conversationId: string
  sender: UserDto
  type: MessageType
  content: string | null
  attachmentUrl: string | null
  sticker: StickerDto | null
  replyTo: ReplyDto | null
  createdAt: string
  isUnsent: boolean
  /** Set when this message replied to a note (text snapshot; the note's owner is the other member). */
  noteReply?: NoteReplyDto | null
}

export interface NoteReplyDto {
  content: string
}

/** Instagram-style note: up to 60 chars, visible to friends for 24 h. */
export interface NoteDto {
  id: string
  user: UserDto
  content: string
  createdAt: string
  expiresAt: string
}

export interface NotesChangedEvent {
  userId: string
}

export interface ConversationDto {
  id: string
  type: ConversationType
  name: string | null
  avatarUrl: string | null
  members: ConversationMemberDto[]
  lastMessage: MessageDto | null
  unreadCount: number
  isReadOnly: boolean
}

export interface SendMessageDto {
  type: Exclude<MessageType, 'System'>
  content?: string | null
  attachmentId?: string
  stickerId?: number
  replyToMessageId?: number | null
}

export interface AttachmentDto {
  attachmentId: string
  url: string
  width: number | null
  height: number | null
}

export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}

// SignalR payloads
export interface MessageUnsentEvent { conversationId: string; messageId: number }
export interface MessageReadEvent { conversationId: string; userId: string; messageId: number }
export interface FriendRemovedEvent { userId: string }
export interface PresenceChangedEvent { userId: string; isOnline: boolean }
export interface UserTypingEvent { conversationId: string; userId: string }

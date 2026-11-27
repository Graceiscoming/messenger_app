import type {
  AuthResultDto, ConversationDto, FriendDto, FriendRequestsDto, FriendshipDto, MessageDto, SendMessageDto,
  StickerPackDto, AttachmentDto, UserDto, MemberRole,
} from '../types/api'
import { request } from './client'
import { PAGE_SIZE } from '../constants'
import { normalizeConversation as norm } from './normalize'

const json = <T>(method: string, path: string, body?: unknown) => request<T>(method, path, { body })

export const authApi = {
  register: (b: { username: string; displayName: string; password: string }) =>
    request<AuthResultDto>('POST', '/auth/register', { body: b, auth: false }),
  login: (b: { username: string; password: string }) =>
    request<AuthResultDto>('POST', '/auth/login', { body: b, auth: false }),
  logout: (refreshToken: string) => request<void>('POST', '/auth/logout', { body: { refreshToken }, auth: false }),
}

export const usersApi = {
  me: () => json<UserDto>('GET', '/users/me'),
  update: (b: { displayName: string; statusMessage: string | null }) => json<UserDto>('PUT', '/users/me', b),
  uploadAvatar: (file: File) => {
    const form = new FormData()
    form.append('file', file)
    return request<UserDto>('PUT', '/users/me/avatar', { form })
  },
  search: (username: string) => request<UserDto[]>('GET', '/users/search', { query: { username } }),
  block: (id: string) => json<void>('POST', `/users/${id}/block`),
  unblock: (id: string) => json<void>('DELETE', `/users/${id}/block`),
}

export const friendsApi = {
  list: () => json<FriendDto[]>('GET', '/friends'),
  remove: (friendId: string) => json<void>('DELETE', `/friends/${friendId}`),
  requests: () => json<FriendRequestsDto>('GET', '/friends/requests'),
  send: (username: string) => json<FriendshipDto>('POST', '/friends/requests', { username }),
  accept: (id: string) => json<void>('POST', `/friends/requests/${id}/accept`),
  decline: (id: string) => json<void>('POST', `/friends/requests/${id}/decline`),
  cancel: (id: string) => json<void>('DELETE', `/friends/requests/${id}`),
}

export const conversationsApi = {
  list: async () => (await json<ConversationDto[]>('GET', '/conversations')).map(norm),
  get: async (id: string) => norm(await json<ConversationDto>('GET', `/conversations/${id}`)),
  direct: async (friendId: string) => norm(await json<ConversationDto>('POST', '/conversations/direct', { friendId })),
  createGroup: async (name: string, memberIds: string[]) =>
    norm(await json<ConversationDto>('POST', '/conversations/group', { name, memberIds })),
  updateGroup: async (id: string, b: { name: string }) =>
    norm(await json<ConversationDto>('PUT', `/conversations/${id}`, b)),
  uploadGroupAvatar: async (id: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return norm(await request<ConversationDto>('PUT', `/conversations/${id}/avatar`, { form }))
  },
  addMembers: (id: string, userIds: string[]) => json<void>('POST', `/conversations/${id}/members`, { userIds }),
  removeMember: (id: string, userId: string) => json<void>('DELETE', `/conversations/${id}/members/${userId}`),
  setRole: (id: string, userId: string, role: MemberRole) =>
    json<void>('PUT', `/conversations/${id}/members/${userId}/role`, { role }),
  leave: (id: string) => json<void>('POST', `/conversations/${id}/leave`),
  markRead: (id: string, messageId: number) => json<void>('POST', `/conversations/${id}/read`, { messageId }),
}

export const messagesApi = {
  history: (convId: string, beforeId?: number) =>
    request<MessageDto[]>('GET', `/conversations/${convId}/messages`, { query: { beforeId, limit: PAGE_SIZE } }),
  send: (convId: string, b: SendMessageDto) => json<MessageDto>('POST', `/conversations/${convId}/messages`, b),
  unsend: (id: number) => json<void>('DELETE', `/messages/${id}`),
}

export const filesApi = {
  uploadImage: (file: File) => {
    const form = new FormData()
    form.append('file', file)
    return request<AttachmentDto>('POST', '/files/images', { form })
  },
  stickers: () => json<StickerPackDto[]>('GET', '/stickers'),
}

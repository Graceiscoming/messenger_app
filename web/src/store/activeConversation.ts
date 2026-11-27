// The conversation currently open in the UI (read by realtime handlers to decide unread increments).
let active: string | null = null
export const activeConversation = {
  get id() { return active },
  set(id: string | null) { active = id },
}

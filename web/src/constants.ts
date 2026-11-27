// Mirrors server config Chat:UnsendWindowHours (docs/08 §3). Hard-coded; keep in sync with the API.
export const UNSEND_WINDOW_HOURS = 24

// Mirrors Chat:PageSize / MaxMessageLength and Storage:MaxImageBytes.
export const PAGE_SIZE = 30
export const MAX_MESSAGE_LENGTH = 2000
export const MAX_CAPTION_LENGTH = 500
export const MAX_IMAGE_BYTES = 5 * 1024 * 1024
export const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/gif']

export const TYPING_THROTTLE_MS = 2000
export const TYPING_EXPIRE_MS = 4000

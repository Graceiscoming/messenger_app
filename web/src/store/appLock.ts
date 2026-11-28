import { useSyncExternalStore } from 'react'

/**
 * Per-device app lock (6-digit PIN), like a chat app's passcode lock.
 * The PIN never leaves the browser: only a salted PBKDF2-SHA256 hash is kept in localStorage, per user.
 * It stops someone casually opening the chats on a shared/unattended device; it is not encryption of
 * the session (see docs/decisions.md).
 */

export const PIN_LENGTH = 6
export const MAX_ATTEMPTS = 5
const COOLDOWN_MS = 30_000
/** Re-lock after the tab has been in the background this long. */
export const RELOCK_AFTER_MS = 5 * 60_000
const ITERATIONS = 150_000

interface StoredPin { v: 1; salt: string; hash: string; iter: number }
interface Fails { count: number; until: number }

const pinKey = (userId: string) => `kubchat.pin.${userId}`
const failKey = (userId: string) => `kubchat.pinFails.${userId}`

/** WebCrypto is only available in secure contexts (https or localhost). */
export const pinSupported = typeof crypto !== 'undefined' && !!crypto.subtle

const b64 = (buf: ArrayBuffer | Uint8Array) => btoa(String.fromCharCode(...new Uint8Array(buf)))
const unb64 = (s: string) => Uint8Array.from(atob(s), (c) => c.charCodeAt(0))

async function derive(pin: string, salt: Uint8Array<ArrayBuffer>, iter: number) {
  const key = await crypto.subtle.importKey('raw', new TextEncoder().encode(pin), 'PBKDF2', false, ['deriveBits'])
  return crypto.subtle.deriveBits({ name: 'PBKDF2', hash: 'SHA-256', salt, iterations: iter }, key, 256)
}

function read<T>(key: string): T | null {
  try { return JSON.parse(localStorage.getItem(key) ?? 'null') as T | null } catch { return null }
}

export const isValidPin = (pin: string) => new RegExp(`^\\d{${PIN_LENGTH}}$`).test(pin)

export function hasPin(userId: string) {
  return read<StoredPin>(pinKey(userId))?.v === 1
}

export async function setPin(userId: string, pin: string) {
  if (!isValidPin(pin)) throw new Error('PIN must be 6 digits')
  const salt = crypto.getRandomValues(new Uint8Array(16))
  const hash = await derive(pin, salt, ITERATIONS)
  const stored: StoredPin = { v: 1, salt: b64(salt), hash: b64(hash), iter: ITERATIONS }
  localStorage.setItem(pinKey(userId), JSON.stringify(stored))
  localStorage.removeItem(failKey(userId))
  locked = false // the user just typed the PIN twice; don't lock them out right away
  emit()
}

export function clearPin(userId: string) {
  try {
    localStorage.removeItem(pinKey(userId))
    localStorage.removeItem(failKey(userId))
  } catch { /* storage unavailable */ }
  locked = false
  emit()
}

/** Milliseconds left before another attempt is allowed (0 = may try now). */
export function cooldownLeft(userId: string) {
  return Math.max(0, (read<Fails>(failKey(userId))?.until ?? 0) - Date.now())
}

export type VerifyResult = { ok: true } | { ok: false; attemptsLeft: number; cooldownMs: number }

/** Checks the PIN, counting failures; after MAX_ATTEMPTS wrong tries the next try waits COOLDOWN_MS. */
export async function verifyPin(userId: string, pin: string): Promise<VerifyResult> {
  const stored = read<StoredPin>(pinKey(userId))
  if (!stored) return { ok: true }
  const wait = cooldownLeft(userId)
  if (wait > 0) return { ok: false, attemptsLeft: 0, cooldownMs: wait }

  const hash = b64(await derive(pin, unb64(stored.salt), stored.iter))
  if (hash === stored.hash) {
    localStorage.removeItem(failKey(userId))
    return { ok: true }
  }
  const prev = read<Fails>(failKey(userId))
  const count = (prev?.count ?? 0) + 1
  if (count >= MAX_ATTEMPTS) {
    localStorage.setItem(failKey(userId), JSON.stringify({ count: 0, until: Date.now() + COOLDOWN_MS }))
    return { ok: false, attemptsLeft: 0, cooldownMs: COOLDOWN_MS }
  }
  localStorage.setItem(failKey(userId), JSON.stringify({ count, until: 0 }))
  return { ok: false, attemptsLeft: MAX_ATTEMPTS - count, cooldownMs: 0 }
}

// ---- lock state (in memory: every page load starts locked when a PIN is set) ----
let locked = true
let version = 0
const listeners = new Set<() => void>()
function emit() { version++; listeners.forEach((l) => l()) }

export const isLocked = (userId: string | null | undefined) => !!userId && locked && hasPin(userId)
export function lockApp() { locked = true; emit() }
export function unlockApp() { locked = false; emit() }

/** Re-renders on lock / PIN changes; returns whether the app is locked for this user. */
export function useAppLock(userId: string) {
  useSyncExternalStore((cb) => { listeners.add(cb); return () => listeners.delete(cb) }, () => version)
  return { locked: isLocked(userId), enabled: hasPin(userId) }
}

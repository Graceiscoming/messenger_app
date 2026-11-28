import { useSyncExternalStore } from 'react'

export type Language = 'th' | 'en'
export type ThemeSetting = 'light' | 'dark' | 'system'
export type FontSize = 'small' | 'medium' | 'large'

export interface Settings {
  language: Language
  theme: ThemeSetting
  enterToSend: boolean
  fontSize: FontSize
  sound: boolean
  desktopNotifications: boolean
}

// Per-device preferences; the inline script in index.html reads the same key to avoid a theme flash.
const KEY = 'messenger.settings'

const defaults: Settings = {
  language: 'th',
  theme: 'system',
  enterToSend: true,
  fontSize: 'medium',
  sound: false,
  desktopNotifications: false,
}

function load(): Settings {
  try {
    const raw = JSON.parse(localStorage.getItem(KEY) ?? '{}') as Partial<Settings>
    return { ...defaults, ...raw }
  } catch {
    return defaults
  }
}

const darkQuery = typeof matchMedia === 'function' ? matchMedia('(prefers-color-scheme: dark)') : null

export function resolveTheme(s: Settings): 'light' | 'dark' {
  return s.theme === 'system' ? (darkQuery?.matches ? 'dark' : 'light') : s.theme
}

function apply(s: Settings) {
  const root = document.documentElement
  root.dataset.theme = resolveTheme(s)
  root.dataset.font = s.fontSize
  root.lang = s.language
}

let state = load()
const listeners = new Set<() => void>()
const emit = () => listeners.forEach((l) => l())

apply(state)
darkQuery?.addEventListener('change', () => {
  if (state.theme === 'system') {
    state = { ...state }
    apply(state)
    emit()
  }
})

export const getSettings = () => state

export function updateSettings(patch: Partial<Settings>) {
  state = { ...state, ...patch }
  try { localStorage.setItem(KEY, JSON.stringify(state)) } catch { /* storage unavailable */ }
  apply(state)
  emit()
}

const subscribe = (l: () => void) => { listeners.add(l); return () => { listeners.delete(l) } }

export const useSettings = (): Settings => useSyncExternalStore(subscribe, getSettings)

import { useEffect, useState } from 'react'
import { fetchBlob } from '../api/client'

// Cache of url -> object URL promise. <img> cannot send the Bearer header, so protected
// images (/api/...) are fetched as blobs. Static assets (e.g. /stickers/...) are used as-is.
const cache = new Map<string, Promise<string>>()

const needsAuth = (url: string) => url.includes('/api/')

export function loadAuthedImage(url: string): Promise<string> {
  let p = cache.get(url)
  if (!p) {
    p = fetchBlob(url).then((b) => URL.createObjectURL(b))
    p.catch(() => cache.delete(url)) // allow retry after failure
    cache.set(url, p)
  }
  return p
}

/** Drop a cached image (e.g. after the avatar behind a stable URL changed). */
export function invalidateAuthedImage(url: string | null | undefined) {
  if (!url) return
  const p = cache.get(url)
  cache.delete(url)
  void p?.then((o) => URL.revokeObjectURL(o)).catch(() => undefined)
}

/** Revoke every cached object URL (call on logout / auth reset). */
export function clearAuthedImages() {
  const all = [...cache.values()]
  cache.clear()
  for (const p of all) void p.then((o) => URL.revokeObjectURL(o)).catch(() => undefined)
}

export function useAuthedImage(url: string | null | undefined): string | null {
  const [state, setState] = useState<{ url: string; src: string } | null>(null)

  useEffect(() => {
    if (!url || !needsAuth(url)) return
    let alive = true
    loadAuthedImage(url).then((src) => { if (alive) setState({ url, src }) }).catch(() => undefined)
    return () => { alive = false }
  }, [url])

  if (!url) return null
  if (!needsAuth(url)) return url
  return state?.url === url ? state.src : null
}

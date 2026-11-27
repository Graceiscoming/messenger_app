import type { AuthResultDto, ProblemDetails } from '../types/api'

const BASE = '/api/v1'
const REFRESH_KEY = 'messenger.refreshToken' // trade-off documented in docs/decisions.md (localStorage)

export class ApiError extends Error {
  status: number
  problem: ProblemDetails | null
  constructor(status: number, problem: ProblemDetails | null, fallback: string) {
    super(problem?.detail || problem?.title || fallback)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

/** Human-readable (Thai fallback) message from any thrown value. */
export function errorMessage(e: unknown): string {
  if (e instanceof ApiError) {
    const fieldErrors = e.problem?.errors ? Object.values(e.problem.errors).flat() : []
    if (fieldErrors.length > 0 && !e.problem?.detail) return fieldErrors.join(' / ')
    if (e.problem?.detail || e.problem?.title) return e.message
    if (e.status === 413) return 'ไฟล์มีขนาดใหญ่เกินไป'
    if (e.status === 403) return 'คุณไม่มีสิทธิ์ทำรายการนี้'
    if (e.status === 404) return 'ไม่พบข้อมูล'
    if (e.status === 409) return 'ไม่สามารถทำรายการได้ในสถานะปัจจุบัน'
    return 'เกิดข้อผิดพลาด กรุณาลองใหม่'
  }
  if (e instanceof TypeError) return 'เชื่อมต่อเซิร์ฟเวอร์ไม่ได้'
  return 'เกิดข้อผิดพลาด กรุณาลองใหม่'
}

// ---- token state (access token in memory only) ----
let accessToken: string | null = null
let expiresAt = 0
let authLostHandler: (() => void) | null = null

export const tokens = {
  get access() { return accessToken },
  get refresh() { try { return localStorage.getItem(REFRESH_KEY) } catch { return null } },
  /** True when the access token is missing or expires within `skewMs`. */
  isStale(skewMs = 30_000) { return !accessToken || Date.now() > expiresAt - skewMs },
  set(r: AuthResultDto) {
    accessToken = r.accessToken
    expiresAt = Date.now() + r.expiresIn * 1000
    try { localStorage.setItem(REFRESH_KEY, r.refreshToken) } catch { /* ignore */ }
  },
  clear() {
    accessToken = null
    expiresAt = 0
    try { localStorage.removeItem(REFRESH_KEY) } catch { /* ignore */ }
  },
}

export function setAuthLostHandler(fn: (() => void) | null) { authLostHandler = fn }

async function parseProblem(res: Response): Promise<ProblemDetails | null> {
  try { return (await res.json()) as ProblemDetails } catch { return null }
}

type RefreshOutcome =
  | { kind: 'ok'; data: AuthResultDto }
  | { kind: 'rejected' } // server said the refresh token is invalid (400/401/403); tokens cleared
  | { kind: 'transient' } // 429/5xx/network error: session is kept

let refreshing: Promise<RefreshOutcome> | null = null

async function refreshOnce(rt: string): Promise<{ outcome: RefreshOutcome; status?: number }> {
  try {
    const res = await fetch(`${BASE}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: rt }),
    })
    if (res.ok) return { outcome: { kind: 'ok', data: (await res.json()) as AuthResultDto } }
    if (res.status === 400 || res.status === 401 || res.status === 403) return { outcome: { kind: 'rejected' }, status: res.status }
    return { outcome: { kind: 'transient' }, status: res.status }
  } catch {
    return { outcome: { kind: 'transient' } }
  }
}

/** Single-flight refresh that distinguishes a rejected token from a transient failure. */
function refreshOutcome(): Promise<RefreshOutcome> {
  if (refreshing) return refreshing
  const rt = tokens.refresh
  if (!rt) return Promise.resolve({ kind: 'rejected' })
  refreshing = (async (): Promise<RefreshOutcome> => {
    try {
      let sent = rt
      let r = await refreshOnce(sent)
      if (r.outcome.kind === 'rejected') {
        const stored = tokens.refresh
        if (stored && stored !== sent) {
          // another tab rotated the token: retry once with the new stored one
          sent = stored
          r = await refreshOnce(sent)
        }
        if (r.outcome.kind === 'rejected' && tokens.refresh === sent) tokens.clear()
      }
      if (r.outcome.kind === 'ok') tokens.set(r.outcome.data)
      return r.outcome
    } finally {
      refreshing = null
    }
  })()
  return refreshing
}

/** Returns the new session, or null when it could not be refreshed (tokens are cleared only if rejected). */
export async function refreshSession(): Promise<AuthResultDto | null> {
  const o = await refreshOutcome()
  return o.kind === 'ok' ? o.data : null
}

async function authedFetch(url: string, init: RequestInit, auth: boolean): Promise<Response> {
  const run = () => {
    const headers = new Headers(init.headers)
    if (auth && tokens.access) headers.set('Authorization', `Bearer ${tokens.access}`)
    return fetch(url, { ...init, headers })
  }
  let res = await run()
  if (res.status === 401 && auth) {
    const r = await refreshOutcome()
    if (r.kind === 'rejected') {
      tokens.clear()
      authLostHandler?.()
      return res
    }
    if (r.kind === 'transient') return res // keep the session; surface the original 401
    res = await run()
  }
  return res
}

interface Options {
  body?: unknown
  form?: FormData
  query?: Record<string, string | number | undefined | null>
  auth?: boolean
}

export async function request<T>(method: string, path: string, opts: Options = {}): Promise<T> {
  let url = BASE + path
  if (opts.query) {
    const qs = new URLSearchParams()
    for (const [k, v] of Object.entries(opts.query)) if (v !== undefined && v !== null && v !== '') qs.set(k, String(v))
    const s = qs.toString()
    if (s) url += `?${s}`
  }
  const init: RequestInit = { method }
  if (opts.form) init.body = opts.form
  else if (opts.body !== undefined) {
    init.body = JSON.stringify(opts.body)
    init.headers = { 'Content-Type': 'application/json' }
  }
  const res = await authedFetch(url, init, opts.auth !== false)
  if (!res.ok) throw new ApiError(res.status, await parseProblem(res), res.statusText)
  if (res.status === 204) return undefined as T
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

/** Fetch a protected file (image/avatar) as a Blob using the Bearer token. */
export async function fetchBlob(url: string): Promise<Blob> {
  const full = /^(https?:)?\/\//.test(url) || url.startsWith('/') ? url : `/${url}`
  const res = await authedFetch(full, {}, true)
  if (!res.ok) throw new ApiError(res.status, null, res.statusText)
  return res.blob()
}

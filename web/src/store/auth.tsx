import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import type { AuthResultDto, UserDto } from '../types/api'
import { AuthContext, type AuthContextValue, type AuthStatus } from './authContext'
import { refreshSession, setAuthLostHandler, tokens } from '../api/client'
import { authApi } from '../api/endpoints'
import { clearAuthedImages } from '../hooks/useAuthedImage'


export function AuthProvider({ children }: { children: ReactNode }) {
  const qc = useQueryClient()
  const [status, setStatus] = useState<AuthStatus>(() => (tokens.refresh ? 'loading' : 'anon'))
  const [user, setUser] = useState<UserDto | null>(null)

  const applyResult = useCallback((r: AuthResultDto) => {
    tokens.set(r)
    setUser(r.user)
    setStatus('authed')
  }, [])

  const reset = useCallback(() => {
    tokens.clear()
    setUser(null)
    setStatus('anon')
    qc.clear()
    clearAuthedImages()
  }, [qc])

  useEffect(() => {
    setAuthLostHandler(reset)
    return () => setAuthLostHandler(null)
  }, [reset])

  useEffect(() => {
    let alive = true
    if (!tokens.refresh) return
    void refreshSession().then((r) => {
      if (!alive) return
      if (r) applyResult(r)
      else setStatus('anon')
    })
    return () => { alive = false }
  }, [applyResult])

  const value = useMemo<AuthContextValue>(() => ({
    status,
    user,
    setUser,
    login: async (username, password) => applyResult(await authApi.login({ username, password })),
    register: async (username, displayName, password) =>
      applyResult(await authApi.register({ username, displayName, password })),
    logout: async () => {
      const rt = tokens.refresh
      try { if (rt) await authApi.logout(rt) } catch { /* best effort */ }
      reset()
    },
  }), [status, user, applyResult, reset])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}


import { createContext, useContext } from 'react'
import type { UserDto } from '../types/api'

export type AuthStatus = 'loading' | 'authed' | 'anon'

export interface AuthContextValue {
  status: AuthStatus
  user: UserDto | null
  login: (username: string, password: string) => Promise<void>
  register: (username: string, displayName: string, password: string) => Promise<void>
  logout: () => Promise<void>
  setUser: (u: UserDto) => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth outside AuthProvider')
  return ctx
}

export function useMe(): UserDto {
  const { user } = useAuth()
  if (!user) throw new Error('not authenticated')
  return user
}

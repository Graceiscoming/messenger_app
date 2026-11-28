import { useCallback, useState, type ReactNode } from 'react'
import { useMe } from '../store/authContext'
import { ProfileViewerContext } from '../store/profileViewer'
import type { UserDto } from '../types/api'
import UserProfileDialog from './UserProfileDialog'

interface Props {
  children: ReactNode
  /** Called instead of showing the card when the user taps their own avatar. */
  onOpenSelf: () => void
  /** Hides the card (e.g. while the PIN lock is up). */
  disabled?: boolean
}

/** Provides useOpenProfile() to the app and renders the profile card of whoever was tapped. */
export default function ProfileViewer({ children, onOpenSelf, disabled }: Props) {
  const me = useMe()
  const [user, setUser] = useState<UserDto | null>(null)
  const open = useCallback((u: UserDto) => {
    if (u.id === me.id) onOpenSelf()
    else setUser(u)
  }, [me.id, onOpenSelf])

  return (
    <ProfileViewerContext.Provider value={open}>
      {children}
      {!disabled && user && <UserProfileDialog key={user.id} user={user} onClose={() => setUser(null)} />}
    </ProfileViewerContext.Provider>
  )
}

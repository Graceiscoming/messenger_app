import type { MouseEvent } from 'react'
import { useT } from '../i18n'
import { useOpenProfile } from '../store/profileViewer'
import type { UserDto } from '../types/api'
import Avatar from './Avatar'

interface Props {
  user: UserDto
  size?: number
  online?: boolean
}

/** An avatar that opens the user's profile card when tapped. */
export default function ProfileAvatar({ user, size, online }: Props) {
  const { t } = useT()
  const openProfile = useOpenProfile()
  const onClick = (e: MouseEvent) => {
    e.stopPropagation() // rows that contain the avatar often have their own click action
    openProfile(user)
  }
  return (
    <button type="button" className="avatar-btn" onClick={onClick} aria-label={t('userProfile.open', { name: user.displayName })}>
      <Avatar url={user.avatarUrl} name={user.displayName} size={size} online={online} />
    </button>
  )
}

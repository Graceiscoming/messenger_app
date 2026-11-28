import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { errorMessage } from '../api/client'
import { conversationsApi, friendsApi, usersApi } from '../api/endpoints'
import { setFriends, upsertConversation, useFriends } from '../hooks/queries'
import { useT } from '../i18n'
import { useMe } from '../store/authContext'
import { useBlocked } from '../store/blocked'
import { useOpenProfile } from '../store/profileViewer'
import type { FriendDto, UserDto } from '../types/api'
import ProfileAvatar from './ProfileAvatar'
import { MoreIcon, SearchIcon, UsersIcon } from './icons'
import { EmptyState, ListSkeleton } from './Skeletons'

export default function FriendsPanel() {
  const { t } = useT()
  const me = useMe()
  const qc = useQueryClient()
  const navigate = useNavigate()
  const { data: friends, isLoading } = useFriends()
  const blocked = useBlocked(me.id)
  const openProfile = useOpenProfile()

  const [q, setQ] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [menuFor, setMenuFor] = useState<string | null>(null)
  const [showBlocked, setShowBlocked] = useState(false)

  const open = async (f: FriendDto) => {
    setError(null)
    try {
      if (f.conversationId) return navigate(`/c/${f.conversationId}`)
      const conv = await conversationsApi.direct(f.user.id)
      upsertConversation(qc, conv)
      setFriends(qc, (l) => l.map((x) => (x.user.id === f.user.id ? { ...x, conversationId: conv.id } : x)))
      navigate(`/c/${conv.id}`)
    } catch (err) { setError(errorMessage(err)) }
  }

  const remove = async (f: FriendDto) => {
    setMenuFor(null)
    if (!window.confirm(t('friends.confirmRemove', { name: f.user.displayName }))) return
    try {
      await friendsApi.remove(f.user.id)
      setFriends(qc, (l) => l.filter((x) => x.user.id !== f.user.id))
    } catch (err) { setError(errorMessage(err)) }
  }

  const block = async (f: FriendDto) => {
    setMenuFor(null)
    if (!window.confirm(t('friends.confirmBlock', { name: f.user.displayName }))) return
    try {
      await usersApi.block(f.user.id)
      blocked.add(f.user)
      setFriends(qc, (l) => l.filter((x) => x.user.id !== f.user.id))
    } catch (err) { setError(errorMessage(err)) }
  }

  const unblock = async (u: UserDto) => {
    try {
      await usersApi.unblock(u.id)
      blocked.remove(u.id)
    } catch (err) { setError(errorMessage(err)) }
  }

  const filtered = (friends ?? []).filter((f) => {
    const term = q.trim().toLowerCase()
    return !term || f.user.displayName.toLowerCase().includes(term) || f.user.username.toLowerCase().includes(term)
  })

  return (
    <div className="panel" onClick={() => setMenuFor(null)}>
      <div className="panel-tools">
        <label className="search-box">
          <SearchIcon />
          <input placeholder={t('friends.filter')} aria-label={t('friends.filter')} value={q} onChange={(e) => setQ(e.target.value)} />
        </label>
      </div>
      {error && <p className="error pad" role="alert">{error}</p>}

      <h3 className="section-title">{t('friends.heading', { n: friends?.length ?? 0 })}</h3>
      {isLoading && <ListSkeleton rows={5} />}
      {!isLoading && filtered.length === 0 && (
        friends?.length
          ? <EmptyState icon={<SearchIcon />} title={t('friends.noMatch')} />
          : <EmptyState icon={<UsersIcon />} title={t('friends.empty')} hint={t('friends.emptyHint')} />
      )}
      <ul className="list">
        {filtered.map((f) => (
          <li key={f.user.id} className="list-item static clickable" onClick={() => open(f)}>
            <ProfileAvatar user={f.user} online={f.isOnline} />
            <div className="list-main">
              <span className="list-title">{f.user.displayName}</span>
              <span className="list-sub">{f.user.statusMessage || `@${f.user.username}`}</span>
            </div>
            <div className="menu-wrap" onClick={(e) => e.stopPropagation()}>
              <button className="icon-btn" aria-label={t('friends.menu')} onClick={() => setMenuFor(menuFor === f.user.id ? null : f.user.id)}><MoreIcon /></button>
              {menuFor === f.user.id && (
                <div className="menu">
                  <button onClick={() => { setMenuFor(null); openProfile(f.user) }}>{t('userProfile.view')}</button>
                  <button onClick={() => remove(f)}>{t('friends.remove')}</button>
                  <button className="danger" onClick={() => block(f)}>{t('friends.block')}</button>
                </div>
              )}
            </div>
          </li>
        ))}
      </ul>

      {blocked.list.length > 0 && (
        <section>
          <h3 className="section-title">
            <button className="link" onClick={() => setShowBlocked(!showBlocked)} aria-expanded={showBlocked}>
              {t('friends.blocked', { n: blocked.list.length })} {showBlocked ? '▲' : '▼'}
            </button>
          </h3>
          {showBlocked && (
            <ul className="list">
              {blocked.list.map((u) => (
                <li key={u.id} className="list-item static">
                  <ProfileAvatar user={u} />
                  <div className="list-main"><span className="list-title">{u.displayName}</span></div>
                  <button className="btn small" onClick={() => unblock(u)}>{t('friends.unblock')}</button>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
    </div>
  )
}

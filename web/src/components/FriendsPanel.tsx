import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { errorMessage } from '../api/client'
import { conversationsApi, friendsApi, usersApi } from '../api/endpoints'
import { keys, setFriends, setRequests, upsertConversation, useFriends, useRequests } from '../hooks/queries'
import { useT } from '../i18n'
import { useMe } from '../store/authContext'
import { useBlocked } from '../store/blocked'
import type { FriendDto, UserDto } from '../types/api'
import Avatar from './Avatar'
import { MoreIcon, SearchIcon, UsersIcon } from './icons'
import { EmptyState, ListSkeleton } from './Skeletons'

export default function FriendsPanel() {
  const { t } = useT()
  const me = useMe()
  const qc = useQueryClient()
  const navigate = useNavigate()
  const { data: friends, isLoading } = useFriends()
  const { data: requests } = useRequests()
  const blocked = useBlocked(me.id)

  const [q, setQ] = useState('')
  const [results, setResults] = useState<UserDto[] | null>(null)
  const [searching, setSearching] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [menuFor, setMenuFor] = useState<string | null>(null)
  const [showBlocked, setShowBlocked] = useState(false)

  const search = async (e: FormEvent) => {
    e.preventDefault()
    const term = q.trim()
    if (!term) return setResults(null)
    setSearching(true)
    setError(null)
    try { setResults(await usersApi.search(term)) } catch (err) { setError(errorMessage(err)) } finally { setSearching(false) }
  }

  const add = async (u: UserDto) => {
    setError(null)
    try {
      const f = await friendsApi.send(u.username)
      if (f.status === 'Pending') setRequests(qc, (r) => ({ ...r, outgoing: [f, ...r.outgoing.filter((x) => x.id !== f.id)] }))
      else { // the other side had already requested us: auto-accepted
        void qc.invalidateQueries({ queryKey: keys.friends })
        void qc.invalidateQueries({ queryKey: keys.requests })
      }
    } catch (err) { setError(errorMessage(err)) }
  }

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

  const isFriend = (id: string) => friends?.some((f) => f.user.id === id)
  const pendingWith = (id: string) =>
    requests?.outgoing.some((r) => r.addressee.id === id) || requests?.incoming.some((r) => r.requester.id === id)
  const filtered = (friends ?? []).filter((f) =>
    !results && f.user.displayName.toLowerCase().includes(q.trim().toLowerCase()))

  return (
    <div className="panel" onClick={() => setMenuFor(null)}>
      <form className="panel-tools" onSubmit={search}>
        <label className="search-box">
          <SearchIcon />
          <input
            placeholder={t('friends.searchPlaceholder')}
            aria-label={t('friends.searchPlaceholder')}
            value={q}
            onChange={(e) => { setQ(e.target.value); if (!e.target.value) setResults(null) }}
          />
        </label>
        <button className="btn small" disabled={searching || !q.trim()}>{t('friends.search')}</button>
      </form>
      {error && <p className="error pad" role="alert">{error}</p>}

      {results && (
        <section>
          <h3 className="section-title">
            {t('friends.results')}
            <button className="link" onClick={() => { setResults(null); setQ('') }}>{t('common.clear')}</button>
          </h3>
          <ul className="list">
            {results.length === 0 && <li className="empty">{t('friends.noUser')}</li>}
            {results.map((u) => (
              <li key={u.id} className="list-item static">
                <Avatar url={u.avatarUrl} name={u.displayName} />
                <div className="list-main">
                  <span className="list-title">{u.displayName}</span>
                  <span className="list-sub">@{u.username}</span>
                </div>
                {isFriend(u.id) ? <span className="muted">{t('friends.isFriend')}</span>
                  : pendingWith(u.id) ? <span className="muted">{t('friends.pending')}</span>
                  : <button className="btn small primary" onClick={() => add(u)}>{t('friends.add')}</button>}
              </li>
            ))}
          </ul>
        </section>
      )}

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
            <Avatar url={f.user.avatarUrl} name={f.user.displayName} online={f.isOnline} />
            <div className="list-main">
              <span className="list-title">{f.user.displayName}</span>
              <span className="list-sub">{f.user.statusMessage || `@${f.user.username}`}</span>
            </div>
            <div className="menu-wrap" onClick={(e) => e.stopPropagation()}>
              <button className="icon-btn" aria-label={t('friends.menu')} onClick={() => setMenuFor(menuFor === f.user.id ? null : f.user.id)}><MoreIcon /></button>
              {menuFor === f.user.id && (
                <div className="menu">
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
                  <Avatar url={u.avatarUrl} name={u.displayName} />
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

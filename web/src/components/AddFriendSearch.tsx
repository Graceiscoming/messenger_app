import { useState, type FormEvent } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { errorMessage } from '../api/client'
import { friendsApi, usersApi } from '../api/endpoints'
import { keys, setRequests, useFriends, useRequests } from '../hooks/queries'
import { useT } from '../i18n'
import type { UserDto } from '../types/api'
import ProfileAvatar from './ProfileAvatar'
import { SearchIcon } from './icons'

/** Search users by username and send friend requests (shown at the top of the Requests tab). */
export default function AddFriendSearch() {
  const { t } = useT()
  const qc = useQueryClient()
  const { data: friends } = useFriends()
  const { data: requests } = useRequests()

  const [q, setQ] = useState('')
  const [results, setResults] = useState<UserDto[] | null>(null)
  const [searching, setSearching] = useState(false)
  const [error, setError] = useState<string | null>(null)

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

  const isFriend = (id: string) => friends?.some((f) => f.user.id === id)
  const pendingWith = (id: string) =>
    requests?.outgoing.some((r) => r.addressee.id === id) || requests?.incoming.some((r) => r.requester.id === id)

  return (
    <section>
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
        <>
          <h3 className="section-title">
            {t('friends.results')}
            <button className="link" onClick={() => { setResults(null); setQ('') }}>{t('common.clear')}</button>
          </h3>
          <ul className="list">
            {results.length === 0 && <li className="empty">{t('friends.noUser')}</li>}
            {results.map((u) => (
              <li key={u.id} className="list-item static">
                <ProfileAvatar user={u} />
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
        </>
      )}
    </section>
  )
}

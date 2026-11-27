import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { errorMessage } from '../api/client'
import { conversationsApi, friendsApi, usersApi } from '../api/endpoints'
import { keys, setFriends, setRequests, upsertConversation, useFriends, useRequests } from '../hooks/queries'
import { useMe } from '../store/auth'
import { useBlocked } from '../store/blocked'
import type { FriendDto, UserDto } from '../types/api'
import Avatar from './Avatar'

export default function FriendsPanel() {
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
    if (!window.confirm(`ลบ ${f.user.displayName} ออกจากเพื่อน?`)) return
    try {
      await friendsApi.remove(f.user.id)
      setFriends(qc, (l) => l.filter((x) => x.user.id !== f.user.id))
    } catch (err) { setError(errorMessage(err)) }
  }

  const block = async (f: FriendDto) => {
    setMenuFor(null)
    if (!window.confirm(`บล็อก ${f.user.displayName}? จะถูกลบออกจากเพื่อนด้วย`)) return
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
    <div className="panel">
      <form className="panel-tools" onSubmit={search}>
        <input
          className="search"
          placeholder="ค้นหา username เพื่อเพิ่มเพื่อน"
          value={q}
          onChange={(e) => { setQ(e.target.value); if (!e.target.value) setResults(null) }}
        />
        <button className="btn small" disabled={searching || !q.trim()}>ค้นหา</button>
      </form>
      {error && <p className="error pad" role="alert">{error}</p>}

      {results && (
        <section>
          <h3 className="section-title">ผลการค้นหา <button className="link" onClick={() => { setResults(null); setQ('') }}>ล้าง</button></h3>
          <ul className="list">
            {results.length === 0 && <li className="empty">ไม่พบผู้ใช้</li>}
            {results.map((u) => (
              <li key={u.id} className="list-item static">
                <Avatar url={u.avatarUrl} name={u.displayName} />
                <div className="list-main">
                  <span className="list-title">{u.displayName}</span>
                  <span className="list-sub">@{u.username}</span>
                </div>
                {isFriend(u.id) ? <span className="muted">เพื่อนแล้ว</span>
                  : pendingWith(u.id) ? <span className="muted">รอการตอบรับ</span>
                  : <button className="btn small primary" onClick={() => add(u)}>เพิ่มเพื่อน</button>}
              </li>
            ))}
          </ul>
        </section>
      )}

      <h3 className="section-title">เพื่อน {friends ? `(${friends.length})` : ''}</h3>
      <ul className="list">
        {isLoading && <li className="empty">กำลังโหลด...</li>}
        {!isLoading && filtered.length === 0 && <li className="empty">{friends?.length ? 'ไม่พบเพื่อน' : 'ยังไม่มีเพื่อน'}</li>}
        {filtered.map((f) => (
          <li key={f.user.id} className="list-item static clickable" onClick={() => open(f)}>
            <Avatar url={f.user.avatarUrl} name={f.user.displayName} online={f.isOnline} />
            <div className="list-main">
              <span className="list-title">{f.user.displayName}</span>
              <span className="list-sub">{f.user.statusMessage || `@${f.user.username}`}</span>
            </div>
            <div className="menu-wrap" onClick={(e) => e.stopPropagation()}>
              <button className="icon-btn" aria-label="เมนู" onClick={() => setMenuFor(menuFor === f.user.id ? null : f.user.id)}>⋯</button>
              {menuFor === f.user.id && (
                <div className="menu">
                  <button onClick={() => remove(f)}>ลบเพื่อน</button>
                  <button className="danger" onClick={() => block(f)}>บล็อก</button>
                </div>
              )}
            </div>
          </li>
        ))}
      </ul>

      {blocked.list.length > 0 && (
        <section>
          <h3 className="section-title">
            <button className="link" onClick={() => setShowBlocked(!showBlocked)}>
              ผู้ใช้ที่บล็อก ({blocked.list.length}) {showBlocked ? '▲' : '▼'}
            </button>
          </h3>
          {showBlocked && (
            <ul className="list">
              {blocked.list.map((u) => (
                <li key={u.id} className="list-item static">
                  <Avatar url={u.avatarUrl} name={u.displayName} />
                  <div className="list-main"><span className="list-title">{u.displayName}</span></div>
                  <button className="btn small" onClick={() => unblock(u)}>เลิกบล็อก</button>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
    </div>
  )
}

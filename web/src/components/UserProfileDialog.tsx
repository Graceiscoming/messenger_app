import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { errorMessage } from '../api/client'
import { conversationsApi, friendsApi, usersApi } from '../api/endpoints'
import {
  keys, setFriends, setRequests, upsertConversation, useConversations, useFriends, useRequests,
} from '../hooks/queries'
import { useT } from '../i18n'
import { useMe } from '../store/authContext'
import { useBlocked } from '../store/blocked'
import type { UserDto } from '../types/api'
import Avatar from './Avatar'
import Modal from './Modal'

/**
 * Another user's profile card: avatar, names, status, presence (friends only), the relationship with
 * actions that fit it, and the groups you share. Built only from data the client already has.
 */
export default function UserProfileDialog({ user: initial, onClose }: { user: UserDto; onClose: () => void }) {
  const { t } = useT()
  const me = useMe()
  const qc = useQueryClient()
  const navigate = useNavigate()
  const { data: friends } = useFriends()
  const { data: requests } = useRequests()
  const { data: convs } = useConversations()
  const blocked = useBlocked(me.id)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const friend = friends?.find((f) => f.user.id === initial.id)
  const user = friend?.user ?? initial // the friends list is kept fresh by SignalR
  const incoming = requests?.incoming.find((r) => r.requester.id === user.id)
  const outgoing = requests?.outgoing.find((r) => r.addressee.id === user.id)
  const isBlocked = blocked.list.some((u) => u.id === user.id)
  const groups = (convs ?? []).filter((c) => c.type === 'Group' && c.members.some((m) => m.user.id === user.id))

  const run = async (fn: () => Promise<void>) => {
    setBusy(true)
    setError(null)
    try { await fn() } catch (err) { setError(errorMessage(err)) } finally { setBusy(false) }
  }

  const dropRequest = (id: string) =>
    setRequests(qc, (r) => ({ incoming: r.incoming.filter((x) => x.id !== id), outgoing: r.outgoing.filter((x) => x.id !== id) }))

  const message = () => run(async () => {
    let convId = friend?.conversationId
    if (!convId) {
      const conv = await conversationsApi.direct(user.id)
      upsertConversation(qc, conv)
      setFriends(qc, (l) => l.map((x) => (x.user.id === user.id ? { ...x, conversationId: conv.id } : x)))
      convId = conv.id
    }
    onClose()
    navigate(`/c/${convId}`)
  })

  const add = () => run(async () => {
    const f = await friendsApi.send(user.username)
    if (f.status === 'Pending') setRequests(qc, (r) => ({ ...r, outgoing: [f, ...r.outgoing.filter((x) => x.id !== f.id)] }))
    else { // they had already requested us: auto-accepted
      void qc.invalidateQueries({ queryKey: keys.friends })
      void qc.invalidateQueries({ queryKey: keys.requests })
    }
  })

  const accept = (id: string) => run(async () => {
    await friendsApi.accept(id)
    dropRequest(id)
    void qc.invalidateQueries({ queryKey: keys.friends })
  })

  const decline = (id: string) => run(async () => { await friendsApi.decline(id); dropRequest(id) })
  const cancel = (id: string) => run(async () => { await friendsApi.cancel(id); dropRequest(id) })

  const remove = () => {
    if (!window.confirm(t('friends.confirmRemove', { name: user.displayName }))) return
    void run(async () => {
      await friendsApi.remove(user.id)
      setFriends(qc, (l) => l.filter((x) => x.user.id !== user.id))
    })
  }

  const block = () => {
    if (!window.confirm(t('friends.confirmBlock', { name: user.displayName }))) return
    void run(async () => {
      await usersApi.block(user.id)
      blocked.add(user)
      setFriends(qc, (l) => l.filter((x) => x.user.id !== user.id))
      void qc.invalidateQueries({ queryKey: keys.requests })
    })
  }

  const unblock = () => run(async () => { await usersApi.unblock(user.id); blocked.remove(user.id) })

  const relation = isBlocked ? t('userProfile.blocked')
    : friend ? t('userProfile.friend')
    : incoming ? t('userProfile.requestReceived')
    : outgoing ? t('userProfile.requestSent')
    : t('userProfile.notFriend')

  return (
    <Modal title={t('userProfile.title')} onClose={onClose}>
      <div className="uprofile">
        <div className="uprofile-cover" aria-hidden="true" />
        <div className="uprofile-avatar">
          <Avatar url={user.avatarUrl} name={user.displayName} size={96} online={friend?.isOnline} />
        </div>
        <h3 className="uprofile-name">{user.displayName}</h3>
        <p className="uprofile-handle">@{user.username}</p>
        {friend && (
          <span className={`uprofile-presence${friend.isOnline ? ' on' : ''}`}>
            {friend.isOnline ? t('common.online') : t('common.offline')}
          </span>
        )}
        {user.statusMessage && <p className="uprofile-status">“{user.statusMessage}”</p>}

        <span className={`uprofile-relation${friend ? ' friend' : ''}${isBlocked ? ' blocked' : ''}`}>{relation}</span>

        <div className="uprofile-actions">
          {isBlocked ? (
            <button className="btn" disabled={busy} onClick={unblock}>{t('friends.unblock')}</button>
          ) : friend ? (<>
            <button className="btn primary" disabled={busy} onClick={message}>{t('userProfile.message')}</button>
            <button className="btn" disabled={busy} onClick={remove}>{t('friends.remove')}</button>
            <button className="btn danger-outline" disabled={busy} onClick={block}>{t('friends.block')}</button>
          </>) : incoming ? (<>
            <button className="btn primary" disabled={busy} onClick={() => accept(incoming.id)}>{t('requests.accept')}</button>
            <button className="btn" disabled={busy} onClick={() => decline(incoming.id)}>{t('requests.decline')}</button>
          </>) : outgoing ? (
            <button className="btn" disabled={busy} onClick={() => cancel(outgoing.id)}>{t('userProfile.cancelRequest')}</button>
          ) : (<>
            <button className="btn primary" disabled={busy} onClick={add}>{t('friends.add')}</button>
            <button className="btn danger-outline" disabled={busy} onClick={block}>{t('friends.block')}</button>
          </>)}
        </div>

        {error && <p className="error" role="alert">{error}</p>}

        {groups.length > 0 && (
          <section className="uprofile-groups">
            <h4 className="section-title flush">{t('userProfile.commonGroups', { n: groups.length })}</h4>
            <ul className="list">
              {groups.map((g) => (
                <li key={g.id} className="list-item static clickable" onClick={() => { onClose(); navigate(`/c/${g.id}`) }}>
                  <Avatar url={g.avatarUrl} name={g.name ?? t('group.default')} size={36} group={!g.avatarUrl} />
                  <div className="list-main">
                    <span className="list-title">{g.name ?? t('group.default')}</span>
                    <span className="list-sub">{t('chat.members', { n: g.members.length })}</span>
                  </div>
                </li>
              ))}
            </ul>
          </section>
        )}
      </div>
    </Modal>
  )
}

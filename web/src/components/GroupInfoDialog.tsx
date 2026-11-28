import { useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { errorMessage } from '../api/client'
import { conversationsApi } from '../api/endpoints'
import { ALLOWED_IMAGE_TYPES, MAX_IMAGE_BYTES } from '../constants'
import { keys, upsertConversation, useConversation, useFriends } from '../hooks/queries'
import { invalidateAuthedImage } from '../hooks/useAuthedImage'
import { useT, type TKey } from '../i18n'
import { useMe } from '../store/authContext'
import type { ConversationDto, MemberRole } from '../types/api'
import Avatar from './Avatar'
import Modal from './Modal'
import { ListSkeleton } from './Skeletons'

const ROLE_KEY: Record<MemberRole, TKey> = { Owner: 'group.role.owner', Admin: 'group.role.admin', Member: 'group.role.member' }

export default function GroupInfoDialog({ conversationId, onClose }: { conversationId: string; onClose: () => void }) {
  const { t } = useT()
  const me = useMe()
  const qc = useQueryClient()
  const navigate = useNavigate()
  const { data: conv } = useConversation(conversationId)
  const { data: friends } = useFriends()
  const [name, setName] = useState<string | null>(null)
  const [adding, setAdding] = useState(false)
  const [picked, setPicked] = useState<Set<string>>(new Set())
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const fileRef = useRef<HTMLInputElement>(null)

  if (!conv) return <Modal title={t('group.info.title')} onClose={onClose}><ListSkeleton rows={4} /></Modal>

  const myRole = conv.members.find((m) => m.user.id === me.id)?.role ?? 'Member'
  const isOwner = myRole === 'Owner'
  const canManage = myRole === 'Owner' || myRole === 'Admin'
  const memberIds = new Set(conv.members.map((m) => m.user.id))
  const candidates = (friends ?? []).filter((f) => !memberIds.has(f.user.id))

  const act = async (fn: () => Promise<void>) => {
    setBusy(true)
    setError(null)
    try {
      await fn()
      // Membership changes also arrive via ConversationChanged; refetch this one room to be sure.
      await qc.invalidateQueries({ queryKey: keys.conversation(conversationId) })
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  const apply = (c: ConversationDto) => upsertConversation(qc, c)

  const rename = () => act(async () => {
    if (name === null || !name.trim()) return
    apply(await conversationsApi.updateGroup(conversationId, { name: name.trim() }))
    setName(null)
  })

  const changeAvatar = (file: File | undefined) => {
    if (!file) return
    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) return setError(t('err.imageType'))
    if (file.size > MAX_IMAGE_BYTES) return setError(t('err.imageSize'))
    void act(async () => {
      const old = conv.avatarUrl
      apply(await conversationsApi.uploadGroupAvatar(conversationId, file))
      invalidateAuthedImage(old)
    })
  }

  const addMembers = () => act(async () => {
    await conversationsApi.addMembers(conversationId, [...picked])
    setPicked(new Set())
    setAdding(false)
  })

  const leave = async () => {
    if (!window.confirm(t('group.confirmLeave'))) return
    setBusy(true)
    try {
      await conversationsApi.leave(conversationId)
      qc.setQueryData<ConversationDto[]>(keys.conversations, (old) => old?.filter((c) => c.id !== conversationId))
      onClose()
      navigate('/')
    } catch (err) {
      setError(errorMessage(err))
      setBusy(false)
    }
  }

  return (
    <Modal title={t('group.info.title')} onClose={onClose} footer={<button className="btn danger-outline" disabled={busy} onClick={leave}>{t('group.leave')}</button>}>
      <div className="profile-avatar">
        <Avatar url={conv.avatarUrl} name={conv.name ?? t('group.default')} size={72} group={!conv.avatarUrl} />
        {canManage && (
          <>
            <button className="btn small" disabled={busy} onClick={() => fileRef.current?.click()}>{t('group.changeAvatar')}</button>
            <input ref={fileRef} type="file" hidden accept={ALLOWED_IMAGE_TYPES.join(',')}
              onChange={(e) => { changeAvatar(e.target.files?.[0]); e.target.value = '' }} />
          </>
        )}
      </div>

      {canManage ? (
        <div className="inline-form">
          <input value={name ?? conv.name ?? ''} maxLength={50} onChange={(e) => setName(e.target.value)} aria-label={t('group.name')} />
          <button className="btn small primary" disabled={busy || name === null || !name.trim() || name === conv.name} onClick={rename}>{t('group.saveName')}</button>
        </div>
      ) : (
        <h3 className="center">{conv.name}</h3>
      )}

      <h3 className="section-title flush">{t('group.members', { n: conv.members.length })}</h3>
      <ul className="list scroll">
        {conv.members.map((m) => {
          const self = m.user.id === me.id
          const canRemove = canManage && !self && m.role !== 'Owner' && (isOwner || m.role === 'Member')
          return (
            <li key={m.user.id} className="list-item static">
              <Avatar url={m.user.avatarUrl} name={m.user.displayName} size={36} />
              <div className="list-main">
                <span className="list-title">{m.user.displayName}{self ? ` (${t('common.you')})` : ''}</span>
                <span className="list-sub">{t(ROLE_KEY[m.role])}</span>
              </div>
              <div className="row-actions">
                {isOwner && !self && m.role !== 'Owner' && (
                  <button className="btn small" disabled={busy}
                    onClick={() => act(() => conversationsApi.setRole(conversationId, m.user.id, m.role === 'Admin' ? 'Member' : 'Admin'))}>
                    {m.role === 'Admin' ? t('group.makeMember') : t('group.makeAdmin')}
                  </button>
                )}
                {canRemove && (
                  <button className="btn small danger-outline" disabled={busy}
                    onClick={() => window.confirm(t('group.confirmRemove', { name: m.user.displayName })) && void act(() => conversationsApi.removeMember(conversationId, m.user.id))}>
                    {t('group.removeMember')}
                  </button>
                )}
              </div>
            </li>
          )
        })}
      </ul>

      {canManage && (
        adding ? (
          <>
            <h3 className="section-title flush">{t('group.addTitle')}</h3>
            <ul className="list scroll">
              {candidates.length === 0 && <li className="empty">{t('group.noCandidates')}</li>}
              {candidates.map((f) => (
                <li key={f.user.id}>
                  <label className="list-item static check">
                    <input type="checkbox" checked={picked.has(f.user.id)}
                      onChange={() => setPicked((s) => { const n = new Set(s); if (n.has(f.user.id)) n.delete(f.user.id); else n.add(f.user.id); return n })} />
                    <Avatar url={f.user.avatarUrl} name={f.user.displayName} size={32} />
                    <span className="list-title">{f.user.displayName}</span>
                  </label>
                </li>
              ))}
            </ul>
            <div className="row-actions end">
              <button className="btn small" onClick={() => { setAdding(false); setPicked(new Set()) }}>{t('common.cancel')}</button>
              <button className="btn small primary" disabled={busy || picked.size === 0} onClick={addMembers}>{t('group.addBtn', { n: picked.size })}</button>
            </div>
          </>
        ) : (
          <button className="btn small" onClick={() => setAdding(true)}>{t('group.add')}</button>
        )
      )}
      {error && <p className="error" role="alert">{error}</p>}
    </Modal>
  )
}

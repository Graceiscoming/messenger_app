import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { errorMessage } from '../api/client'
import { conversationsApi } from '../api/endpoints'
import { upsertConversation, useFriends } from '../hooks/queries'
import { useT } from '../i18n'
import Avatar from './Avatar'
import Modal from './Modal'
import { ListSkeleton } from './Skeletons'

export default function CreateGroupDialog({ onClose }: { onClose: () => void }) {
  const { t } = useT()
  const qc = useQueryClient()
  const navigate = useNavigate()
  const { data: friends, isLoading } = useFriends()
  const [name, setName] = useState('')
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const toggle = (id: string) =>
    setSelected((s) => { const n = new Set(s); if (n.has(id)) n.delete(id); else n.add(id); return n })

  const valid = name.trim().length >= 1 && name.trim().length <= 50 && selected.size >= 1

  const create = async () => {
    setBusy(true)
    setError(null)
    try {
      const conv = await conversationsApi.createGroup(name.trim(), [...selected])
      upsertConversation(qc, conv)
      onClose()
      navigate(`/c/${conv.id}`)
    } catch (err) {
      setError(errorMessage(err))
      setBusy(false)
    }
  }

  return (
    <Modal
      title={t('group.create.title')}
      onClose={onClose}
      footer={<>
        <button className="btn" onClick={onClose}>{t('common.cancel')}</button>
        <button className="btn primary" disabled={!valid || busy} onClick={create}>{t('group.createBtn', { n: selected.size })}</button>
      </>}
    >
      <label className="field">{t('group.name')}
        <input value={name} maxLength={50} onChange={(e) => setName(e.target.value)} autoFocus />
      </label>
      <h3 className="section-title flush">{t('group.pickFriends')}</h3>
      {isLoading && <ListSkeleton rows={3} />}
      {!isLoading && !friends?.length && <p className="empty">{t('group.noFriends')}</p>}
      <ul className="list scroll">
        {friends?.map((f) => (
          <li key={f.user.id}>
            <label className="list-item static check">
              <input type="checkbox" checked={selected.has(f.user.id)} onChange={() => toggle(f.user.id)} />
              <Avatar url={f.user.avatarUrl} name={f.user.displayName} size={36} />
              <span className="list-title">{f.user.displayName}</span>
            </label>
          </li>
        ))}
      </ul>
      {error && <p className="error" role="alert">{error}</p>}
    </Modal>
  )
}

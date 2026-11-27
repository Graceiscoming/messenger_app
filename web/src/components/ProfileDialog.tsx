import { useRef, useState } from 'react'
import { errorMessage } from '../api/client'
import { usersApi } from '../api/endpoints'
import { ALLOWED_IMAGE_TYPES, MAX_IMAGE_BYTES } from '../constants'
import { invalidateAuthedImage } from '../hooks/useAuthedImage'
import { useAuth, useMe } from '../store/auth'
import Avatar from './Avatar'
import Modal from './Modal'

export default function ProfileDialog({ onClose }: { onClose: () => void }) {
  const me = useMe()
  const { setUser, logout } = useAuth()
  const [displayName, setDisplayName] = useState(me.displayName)
  const [statusMessage, setStatusMessage] = useState(me.statusMessage ?? '')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const fileRef = useRef<HTMLInputElement>(null)

  const pickAvatar = async (file: File | undefined) => {
    if (!file) return
    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) return setError('รองรับเฉพาะไฟล์ jpeg, png, webp, gif')
    if (file.size > MAX_IMAGE_BYTES) return setError('ไฟล์ต้องมีขนาดไม่เกิน 5 MB')
    setError(null)
    setBusy(true)
    try {
      const old = me.avatarUrl
      const updated = await usersApi.uploadAvatar(file)
      invalidateAuthedImage(old)
      invalidateAuthedImage(updated.avatarUrl)
      setUser(updated)
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setBusy(false)
      if (fileRef.current) fileRef.current.value = ''
    }
  }

  const save = async () => {
    setBusy(true)
    setError(null)
    try {
      setUser(await usersApi.update({ displayName: displayName.trim(), statusMessage: statusMessage.trim() || null }))
      onClose()
    } catch (err) {
      setError(errorMessage(err))
      setBusy(false)
    }
  }

  return (
    <Modal
      title="โปรไฟล์ของฉัน"
      onClose={onClose}
      footer={<>
        <button className="btn danger-outline" onClick={() => void logout()}>ออกจากระบบ</button>
        <span className="spacer" />
        <button className="btn" onClick={onClose}>ยกเลิก</button>
        <button className="btn primary" disabled={busy || !displayName.trim()} onClick={save}>บันทึก</button>
      </>}
    >
      <div className="profile-avatar">
        <Avatar url={me.avatarUrl} name={me.displayName} size={88} />
        <button className="btn small" disabled={busy} onClick={() => fileRef.current?.click()}>เปลี่ยนรูปโปรไฟล์</button>
        <input ref={fileRef} type="file" hidden accept={ALLOWED_IMAGE_TYPES.join(',')} onChange={(e) => void pickAvatar(e.target.files?.[0])} />
      </div>
      <p className="muted center">@{me.username}</p>
      <label className="field">ชื่อที่แสดง
        <input value={displayName} maxLength={50} onChange={(e) => setDisplayName(e.target.value)} />
      </label>
      <label className="field">สถานะ
        <input value={statusMessage} maxLength={100} onChange={(e) => setStatusMessage(e.target.value)} placeholder="เช่น ว่างอยู่" />
      </label>
      {error && <p className="error" role="alert">{error}</p>}
    </Modal>
  )
}

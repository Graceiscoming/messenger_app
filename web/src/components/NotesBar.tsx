import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { errorMessage } from '../api/client'
import { notesApi } from '../api/endpoints'
import { addMessageToCache, keys, useNotes } from '../hooks/queries'
import { useT } from '../i18n'
import { useMe } from '../store/authContext'
import type { NoteDto, UserDto } from '../types/api'
import Avatar from './Avatar'
import { PlusIcon, SendIcon } from './icons'
import Modal from './Modal'

const MAX = 60

/**
 * Instagram-style notes row at the top of the chat list: the user's own note first, then friends'.
 * Notes are up to 60 characters and disappear after 24 hours; tapping a friend's note lets you reply in their chat.
 */
export default function NotesBar() {
  const { t } = useT()
  const me = useMe()
  const { data } = useNotes()
  const [editing, setEditing] = useState(false)
  const [viewing, setViewing] = useState<NoteDto | null>(null)

  const mine = data?.find((n) => n.user.id === me.id) ?? null
  const others = (data ?? []).filter((n) => n.user.id !== me.id)

  return (
    <>
      <ul className="notes-bar" aria-label={t('notes.row')}>
        <li>
          <button className="note-item" onClick={() => setEditing(true)}>
            <span className={`note-bubble${mine ? '' : ' empty'}`}>{mine ? mine.content : <><PlusIcon /> {t('notes.add')}</>}</span>
            <Avatar url={me.avatarUrl} name={me.displayName} size={56} />
            <span className="note-name">{t('notes.yours')}</span>
          </button>
        </li>
        {others.map((n) => (
          <li key={n.id}>
            <button className="note-item" onClick={() => setViewing(n)}>
              <span className="note-bubble">{n.content}</span>
              <Avatar url={n.user.avatarUrl} name={n.user.displayName} size={56} />
              <span className="note-name">{n.user.displayName}</span>
            </button>
          </li>
        ))}
      </ul>
      {editing && <NoteEditor me={me} current={mine} onClose={() => setEditing(false)} />}
      {viewing && <NoteReply note={viewing} onClose={() => setViewing(null)} />}
    </>
  )
}

function useAgo() {
  const { t } = useT()
  return (iso: string) => {
    const mins = Math.floor((Date.now() - Date.parse(iso)) / 60_000)
    if (mins < 1) return t('notes.justNow')
    if (mins < 60) return t('notes.minutesAgo', { n: mins })
    return t('notes.hoursAgo', { n: Math.floor(mins / 60) })
  }
}

function NoteEditor({ me, current, onClose }: { me: UserDto; current: NoteDto | null; onClose: () => void }) {
  const { t } = useT()
  const qc = useQueryClient()
  const ago = useAgo()
  const [text, setText] = useState(current?.content ?? '')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const run = async (fn: () => Promise<unknown>) => {
    setBusy(true); setError(null)
    try { await fn(); await qc.invalidateQueries({ queryKey: keys.notes }); onClose() }
    catch (err) { setError(errorMessage(err)) }
    finally { setBusy(false) }
  }
  const share = (e?: FormEvent) => { e?.preventDefault(); if (text.trim()) void run(() => notesApi.set(text.trim())) }
  const [openedAt] = useState(() => Date.now()) // fixed while the dialog is open
  const hoursLeft = current ? Math.max(1, Math.ceil((Date.parse(current.expiresAt) - openedAt) / 3_600_000)) : 0

  return (
    <Modal
      title={t('notes.title')} onClose={onClose}
      footer={<>
        {current && <button className="btn danger-outline" disabled={busy} onClick={() => void run(notesApi.remove)}>{t('notes.delete')}</button>}
        <span className="spacer" />
        <button className="btn" onClick={onClose}>{t('common.cancel')}</button>
        <button className="btn primary" disabled={busy || !text.trim() || text.trim() === current?.content} onClick={() => share()}>{t('notes.share')}</button>
      </>}
    >
      <form className="note-editor" onSubmit={share}>
        <div className="note-preview">
          <span className="note-bubble big">{text.trim() || <span className="muted">{t('notes.placeholder')}</span>}</span>
          <Avatar url={me.avatarUrl} name={me.displayName} size={72} />
        </div>
        <input
          autoFocus value={text} maxLength={MAX} placeholder={t('notes.placeholder')} aria-label={t('notes.placeholder')}
          onChange={(e) => setText(e.target.value)}
        />
        <div className="note-meta">
          <span>{current ? `${ago(current.createdAt)} · ${t('notes.expiresIn', { n: hoursLeft })}` : t('notes.hint')}</span>
          <span className={text.length >= MAX ? 'warn' : ''}>{text.length}/{MAX}</span>
        </div>
        {error && <p className="error" role="alert">{error}</p>}
      </form>
    </Modal>
  )
}

function NoteReply({ note, onClose }: { note: NoteDto; onClose: () => void }) {
  const { t } = useT()
  const qc = useQueryClient()
  const navigate = useNavigate()
  const ago = useAgo()
  const [text, setText] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const send = async (e: FormEvent) => {
    e.preventDefault()
    if (!text.trim()) return
    setBusy(true); setError(null)
    try {
      const msg = await notesApi.reply(note.id, text.trim())
      addMessageToCache(qc, msg)
      void qc.invalidateQueries({ queryKey: keys.conversations })
      onClose()
      navigate(`/c/${msg.conversationId}`)
    } catch (err) { setError(errorMessage(err)) } finally { setBusy(false) }
  }

  return (
    <Modal title={t('notes.replyTo', { name: note.user.displayName })} onClose={onClose}>
      <div className="note-preview">
        <span className="note-bubble big">{note.content}</span>
        <Avatar url={note.user.avatarUrl} name={note.user.displayName} size={72} />
        <strong>{note.user.displayName}</strong>
        <span className="muted">{ago(note.createdAt)}</span>
      </div>
      <form className="note-reply-form" onSubmit={send}>
        <input
          autoFocus value={text} maxLength={4000} placeholder={t('notes.replyPlaceholder', { name: note.user.displayName })}
          aria-label={t('notes.replyPlaceholder', { name: note.user.displayName })} onChange={(e) => setText(e.target.value)}
        />
        <button className="icon-btn filled" disabled={busy || !text.trim()} aria-label={t('chat.send')} title={t('chat.send')}><SendIcon /></button>
      </form>
      {error && <p className="error" role="alert">{error}</p>}
    </Modal>
  )
}

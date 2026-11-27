import { useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { ApiError, errorMessage } from '../api/client'
import { filesApi, messagesApi } from '../api/endpoints'
import {
  ALLOWED_IMAGE_TYPES, MAX_CAPTION_LENGTH, MAX_IMAGE_BYTES, MAX_MESSAGE_LENGTH, TYPING_THROTTLE_MS,
} from '../constants'
import { sendTyping } from '../hubs/chatHub'
import { addMessageToCache, patchConversation } from '../hooks/queries'
import type { MessageDto, SendMessageDto } from '../types/api'
import StickerPicker from './StickerPicker'

interface Props {
  conversationId: string
  disabled: boolean
  reply: MessageDto | null
  onClearReply: () => void
  /** Called when the server answers 403 (e.g. the friend was removed -> room is read-only). */
  onForbidden: (message: string) => void
  onSent: () => void
}

export default function Composer({ conversationId, disabled, reply, onClearReply, onForbidden, onSent }: Props) {
  const qc = useQueryClient()
  const [text, setText] = useState('')
  const [image, setImage] = useState<{ file: File; preview: string } | null>(null)
  const [showStickers, setShowStickers] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const fileRef = useRef<HTMLInputElement>(null)
  const areaRef = useRef<HTMLTextAreaElement>(null)
  const lastTyping = useRef(0)

  useEffect(() => () => { if (image) URL.revokeObjectURL(image.preview) }, [image])
  useEffect(() => { if (reply) areaRef.current?.focus() }, [reply])

  const send = async (body: SendMessageDto): Promise<boolean> => {
    setBusy(true)
    setError(null)
    try {
      const msg = await messagesApi.send(conversationId, { ...body, replyToMessageId: reply?.id ?? null })
      addMessageToCache(qc, msg)
      patchConversation(qc, conversationId, (c) => ({ ...c, lastMessage: msg }))
      onClearReply()
      onSent()
      return true
    } catch (err) {
      if (err instanceof ApiError && err.status === 403) onForbidden(errorMessage(err))
      else setError(errorMessage(err))
      return false
    } finally {
      setBusy(false)
    }
  }

  const submit = async () => {
    if (busy || disabled) return
    const content = text.trim()
    if (image) {
      if (content.length > MAX_CAPTION_LENGTH) return setError(`คำบรรยายต้องไม่เกิน ${MAX_CAPTION_LENGTH} ตัวอักษร`)
      setBusy(true)
      try {
        const up = await filesApi.uploadImage(image.file)
        setBusy(false)
        if (await send({ type: 'Image', attachmentId: up.attachmentId, content: content || null })) {
          setImage(null)
          setText('')
        }
      } catch (err) {
        setBusy(false)
        setError(errorMessage(err))
      }
      return
    }
    if (!content) return
    if (content.length > MAX_MESSAGE_LENGTH) return setError(`ข้อความต้องไม่เกิน ${MAX_MESSAGE_LENGTH} ตัวอักษร`)
    if (await send({ type: 'Text', content })) setText('')
  }

  const pickImage = (file: File | undefined) => {
    if (!file) return
    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) return setError('รองรับเฉพาะไฟล์ jpeg, png, webp, gif')
    if (file.size > MAX_IMAGE_BYTES) return setError('ไฟล์ต้องมีขนาดไม่เกิน 5 MB')
    setError(null)
    setImage({ file, preview: URL.createObjectURL(file) })
  }

  const onChange = (v: string) => {
    setText(v)
    const now = Date.now()
    if (v.trim() && now - lastTyping.current > TYPING_THROTTLE_MS) {
      lastTyping.current = now
      sendTyping(conversationId)
    }
  }

  return (
    <div className="composer">
      {showStickers && !disabled && (
        <StickerPicker onPick={(s) => { setShowStickers(false); void send({ type: 'Sticker', stickerId: s.id }) }} />
      )}
      {reply && (
        <div className="reply-bar">
          <span>ตอบกลับ <b>{reply.sender.displayName}</b>: {reply.type === 'Text' ? reply.content : reply.type === 'Image' ? '[รูปภาพ]' : '[สติกเกอร์]'}</span>
          <button className="icon-btn" onClick={onClearReply} aria-label="ยกเลิกการตอบกลับ">✕</button>
        </div>
      )}
      {image && (
        <div className="image-preview">
          <img src={image.preview} alt="ตัวอย่างรูป" />
          <button className="icon-btn" onClick={() => setImage(null)} aria-label="เอารูปออก">✕</button>
        </div>
      )}
      {error && <p className="error pad" role="alert">{error}</p>}
      <div className="composer-row">
        <button className="icon-btn" disabled={disabled || busy} title="แนบรูป" onClick={() => fileRef.current?.click()}>🖼️</button>
        <input ref={fileRef} type="file" hidden accept={ALLOWED_IMAGE_TYPES.join(',')}
          onChange={(e) => { pickImage(e.target.files?.[0]); e.target.value = '' }} />
        <button className={`icon-btn${showStickers ? ' on' : ''}`} disabled={disabled || busy} title="สติกเกอร์" onClick={() => setShowStickers((v) => !v)}>😊</button>
        <textarea
          ref={areaRef}
          rows={1}
          value={text}
          disabled={disabled}
          placeholder={disabled ? 'ไม่สามารถส่งข้อความได้' : image ? 'เพิ่มคำบรรยาย (ไม่บังคับ)' : 'พิมพ์ข้อความ'}
          onChange={(e) => onChange(e.target.value)}
          onKeyDown={(e) => {
            // isComposing: do not send while confirming a Thai/IME composition
            if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) { e.preventDefault(); void submit() }
          }}
        />
        <button className="btn primary send" disabled={disabled || busy || (!text.trim() && !image)} onClick={() => void submit()}>ส่ง</button>
      </div>
    </div>
  )
}

import { useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { ApiError, errorMessage } from '../api/client'
import { filesApi, messagesApi } from '../api/endpoints'
import {
  ALLOWED_IMAGE_TYPES, MAX_CAPTION_LENGTH, MAX_IMAGE_BYTES, MAX_MESSAGE_LENGTH, TYPING_THROTTLE_MS,
} from '../constants'
import { sendTyping } from '../hubs/chatHub'
import { addMessageToCache, patchConversation } from '../hooks/queries'
import { useT } from '../i18n'
import { useSettings } from '../store/settings'
import type { MessageDto, SendMessageDto } from '../types/api'
import { CloseIcon, ImageIcon, SendIcon, SmileIcon } from './icons'
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
  const { t } = useT()
  const { enterToSend } = useSettings()
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

  // Auto-grow the textarea up to a max height.
  useEffect(() => {
    const el = areaRef.current
    if (!el) return
    el.style.height = 'auto'
    el.style.height = `${Math.min(el.scrollHeight, 140)}px`
  }, [text])

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
      if (content.length > MAX_CAPTION_LENGTH) return setError(t('err.captionTooLong', { n: MAX_CAPTION_LENGTH }))
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
    if (content.length > MAX_MESSAGE_LENGTH) return setError(t('err.messageTooLong', { n: MAX_MESSAGE_LENGTH }))
    if (await send({ type: 'Text', content })) setText('')
  }

  const pickImage = (file: File | undefined) => {
    if (!file) return
    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) return setError(t('err.imageType'))
    if (file.size > MAX_IMAGE_BYTES) return setError(t('err.imageSize'))
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

  const replyText = reply
    ? reply.type === 'Text' ? reply.content : reply.type === 'Image' ? t('preview.image') : t('preview.sticker')
    : ''

  return (
    <div className="composer">
      {showStickers && !disabled && (
        <StickerPicker onPick={(s) => { setShowStickers(false); void send({ type: 'Sticker', stickerId: s.id }) }} />
      )}
      {reply && (
        <div className="reply-bar">
          <span>{t('chat.replyTo')} <b>{reply.sender.displayName}</b>: {replyText}</span>
          <button className="icon-btn" onClick={onClearReply} aria-label={t('chat.cancelReply')}><CloseIcon /></button>
        </div>
      )}
      {image && (
        <div className="image-preview">
          <img src={image.preview} alt={t('chat.imagePreview')} />
          <button className="icon-btn" onClick={() => setImage(null)} aria-label={t('chat.removeImage')}><CloseIcon /></button>
        </div>
      )}
      {error && <p className="error pad" role="alert">{error}</p>}
      <div className="composer-row">
        <button className="icon-btn" disabled={disabled || busy} title={t('chat.attach')} aria-label={t('chat.attach')} onClick={() => fileRef.current?.click()}><ImageIcon /></button>
        <input ref={fileRef} type="file" hidden accept={ALLOWED_IMAGE_TYPES.join(',')}
          onChange={(e) => { pickImage(e.target.files?.[0]); e.target.value = '' }} />
        <button className={`icon-btn${showStickers ? ' on' : ''}`} disabled={disabled || busy} title={t('chat.stickers')} aria-label={t('chat.stickers')} onClick={() => setShowStickers((v) => !v)}><SmileIcon /></button>
        <textarea
          ref={areaRef}
          rows={1}
          value={text}
          disabled={disabled}
          aria-label={t('chat.placeholder')}
          placeholder={disabled ? t('chat.sendDisabled') : image ? t('chat.captionPlaceholder') : enterToSend ? t('chat.placeholder') : t('chat.placeholderCtrl')}
          onChange={(e) => onChange(e.target.value)}
          onKeyDown={(e) => {
            // isComposing: never send while confirming a Thai/IME composition
            if (e.key !== 'Enter' || e.nativeEvent.isComposing) return
            const modifier = e.ctrlKey || e.metaKey
            if (enterToSend ? !e.shiftKey && !modifier : modifier) { e.preventDefault(); void submit() }
          }}
        />
        <button className="btn primary send" disabled={disabled || busy || (!text.trim() && !image)} onClick={() => void submit()} aria-label={t('chat.send')}>
          <SendIcon /> <span className="send-label">{t('chat.send')}</span>
        </button>
      </div>
    </div>
  )
}

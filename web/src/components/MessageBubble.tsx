import { UNSEND_WINDOW_HOURS } from '../constants'
import type { MessageDto } from '../types/api'
import { formatTime, parseDate } from '../utils/format'
import Avatar from './Avatar'
import AuthedImage from './AuthedImage'

interface Props {
  msg: MessageDto
  mine: boolean
  isGroup: boolean
  /** Read receipt label ("อ่านแล้ว" / "อ่านแล้ว N") or null. Only passed for own messages. */
  readLabel: string | null
  menuOpen: boolean
  onToggleMenu: () => void
  onReply: () => void
  onUnsend: () => void
  onImageClick: (src: string) => void
  onMediaLoad: () => void
}

function withinUnsendWindow(m: MessageDto) {
  return !m.isUnsent && Date.now() - parseDate(m.createdAt).getTime() < UNSEND_WINDOW_HOURS * 3_600_000
}

function ReplyQuote({ msg }: { msg: MessageDto }) {
  const r = msg.replyTo
  if (!r) return null
  const text = r.isUnsent ? 'ข้อความนี้ถูกยกเลิกการส่ง' : r.type === 'Image' ? '[รูปภาพ]' : r.type === 'Sticker' ? '[สติกเกอร์]' : r.content ?? ''
  return (
    <div className="reply-quote">
      <b>{r.senderDisplayName}</b>
      <span>{text}</span>
    </div>
  )
}

export default function MessageBubble({
  msg, mine, isGroup, readLabel, menuOpen, onToggleMenu, onReply, onUnsend, onImageClick, onMediaLoad,
}: Props) {
  if (msg.type === 'System') {
    return <div className="system-msg"><span>{msg.content}</span></div>
  }

  const unsendable = mine && withinUnsendWindow(msg)
  const body = msg.isUnsent ? (
    <div className="bubble unsent">ข้อความนี้ถูกยกเลิกการส่ง</div>
  ) : msg.type === 'Sticker' && msg.sticker ? (
    <img className="sticker" src={msg.sticker.imageUrl} alt={msg.sticker.code} onLoad={onMediaLoad} />
  ) : (
    <div className="bubble">
      <ReplyQuote msg={msg} />
      {msg.type === 'Image' && msg.attachmentUrl && (
        <AuthedImage url={msg.attachmentUrl} className="msg-image" alt="รูปภาพ" onLoad={onMediaLoad} onClick={onImageClick} />
      )}
      {msg.content && <p className="msg-text">{msg.content}</p>}
    </div>
  )

  return (
    <div className={`msg-row ${mine ? 'mine' : 'theirs'}`}>
      {!mine && <Avatar url={msg.sender.avatarUrl} name={msg.sender.displayName} size={34} />}
      <div className="msg-col">
        {!mine && isGroup && <span className="sender-name">{msg.sender.displayName}</span>}
        <div className="msg-line">
          {mine && <MetaBlock readLabel={readLabel} time={msg.createdAt} />}
          {body}
          {!mine && <MetaBlock readLabel={null} time={msg.createdAt} />}
          {!msg.isUnsent && (
            <div className="menu-wrap msg-menu">
              <button className="icon-btn" aria-label="เมนูข้อความ" onClick={(e) => { e.stopPropagation(); onToggleMenu() }}>⋯</button>
              {menuOpen && (
                <div className={`menu ${mine ? 'left' : ''}`}>
                  <button onClick={onReply}>ตอบกลับ</button>
                  {unsendable && <button className="danger" onClick={onUnsend}>ยกเลิกการส่ง</button>}
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

function MetaBlock({ readLabel, time }: { readLabel: string | null; time: string }) {
  return (
    <span className="meta">
      {readLabel && <span className="read">{readLabel}</span>}
      <span>{formatTime(time)}</span>
    </span>
  )
}

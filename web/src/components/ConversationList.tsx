import { useState } from 'react'
import { NavLink } from 'react-router-dom'
import { useConversations, useFriends } from '../hooks/queries'
import { useMe } from '../store/auth'
import { convAvatar, convTitle, formatListTime, messagePreview, otherMember } from '../utils/format'
import Avatar from './Avatar'

export default function ConversationList({ onCreateGroup }: { onCreateGroup: () => void }) {
  const me = useMe()
  const { data, isLoading, isError } = useConversations()
  const { data: friends } = useFriends()
  const [q, setQ] = useState('')

  const list = (data ?? []).filter((c) => convTitle(c, me.id).toLowerCase().includes(q.trim().toLowerCase()))

  return (
    <div className="panel">
      <div className="panel-tools">
        <input className="search" placeholder="ค้นหาแชท" value={q} onChange={(e) => setQ(e.target.value)} />
        <button className="btn small" onClick={onCreateGroup} title="สร้างกลุ่มใหม่">+ กลุ่ม</button>
      </div>
      <ul className="list">
        {isLoading && <li className="empty">กำลังโหลด...</li>}
        {isError && <li className="empty error">โหลดรายการแชทไม่สำเร็จ</li>}
        {!isLoading && list.length === 0 && <li className="empty">{q ? 'ไม่พบแชท' : 'ยังไม่มีแชท เริ่มจากเพิ่มเพื่อนได้เลย'}</li>}
        {list.map((c) => {
          const other = c.type === 'Direct' ? otherMember(c, me.id) : null
          const online = other ? friends?.find((f) => f.user.id === other.id)?.isOnline : undefined
          return (
            <li key={c.id}>
              <NavLink to={`/c/${c.id}`} className={({ isActive }) => `list-item${isActive ? ' active' : ''}`}>
                <Avatar url={convAvatar(c, me.id)} name={convTitle(c, me.id)} group={c.type === 'Group' && !c.avatarUrl} online={online} />
                <div className="list-main">
                  <div className="list-top">
                    <span className="list-title">{convTitle(c, me.id)}</span>
                    {c.lastMessage && <span className="list-time">{formatListTime(c.lastMessage.createdAt)}</span>}
                  </div>
                  <div className="list-bottom">
                    <span className="list-sub">{messagePreview(c.lastMessage, me.id)}</span>
                    {c.unreadCount > 0 && <span className="badge">{c.unreadCount > 99 ? '99+' : c.unreadCount}</span>}
                  </div>
                </div>
              </NavLink>
            </li>
          )
        })}
      </ul>
    </div>
  )
}

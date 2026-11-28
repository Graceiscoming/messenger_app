import { useState } from 'react'
import { NavLink } from 'react-router-dom'
import { useConversations, useFriends } from '../hooks/queries'
import { useT } from '../i18n'
import { useMe } from '../store/authContext'
import { convAvatar, convTitle, formatListTime, messagePreview, otherMember } from '../utils/format'
import Avatar from './Avatar'
import { ChatIcon, SearchIcon } from './icons'
import NotesBar from './NotesBar'
import { EmptyState, ListSkeleton } from './Skeletons'

export default function ConversationList() {
  const { t } = useT()
  const me = useMe()
  const { data, isLoading, isError } = useConversations()
  const { data: friends } = useFriends()
  const [q, setQ] = useState('')

  const list = (data ?? []).filter((c) => convTitle(c, me.id).toLowerCase().includes(q.trim().toLowerCase()))

  return (
    <div className="panel">
      <div className="panel-tools">
        <label className="search-box">
          <SearchIcon />
          <input placeholder={t('chats.search')} aria-label={t('chats.search')} value={q} onChange={(e) => setQ(e.target.value)} />
        </label>
      </div>
      {!q && <NotesBar />}
      {isLoading && <ListSkeleton />}
      {isError && <p className="empty error">{t('chats.loadError')}</p>}
      {!isLoading && !isError && list.length === 0 && (
        q
          ? <EmptyState icon={<SearchIcon />} title={t('chats.noMatch')} />
          : <EmptyState icon={<ChatIcon />} title={t('chats.empty')} hint={t('chats.emptyHint')} />
      )}
      <ul className="list">
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
                    <span className={`list-sub${c.unreadCount > 0 ? ' strong' : ''}`}>{messagePreview(c.lastMessage, me.id)}</span>
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

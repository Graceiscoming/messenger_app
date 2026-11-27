import { useState } from 'react'
import { Navigate, Outlet, useMatch } from 'react-router-dom'
import Avatar from '../components/Avatar'
import ConversationList from '../components/ConversationList'
import CreateGroupDialog from '../components/CreateGroupDialog'
import FriendsPanel from '../components/FriendsPanel'
import ProfileDialog from '../components/ProfileDialog'
import RequestsPanel from '../components/RequestsPanel'
import { useConversations, useRequests } from '../hooks/queries'
import { useRealtime } from '../hubs/useRealtime'
import { useAuth } from '../store/auth'

type Tab = 'chats' | 'friends' | 'requests'

export default function MainLayout() {
  const { status, user } = useAuth()
  if (status === 'loading') return <div className="splash">กำลังโหลด...</div>
  if (status === 'anon' || !user) return <Navigate to="/login" replace />
  return <Shell meId={user.id} />
}

function Shell({ meId }: { meId: string }) {
  const { user } = useAuth()
  useRealtime(meId)
  const inChat = !!useMatch('/c/:id')
  const [tab, setTab] = useState<Tab>('chats')
  const [showGroup, setShowGroup] = useState(false)
  const [showProfile, setShowProfile] = useState(false)
  const { data: requests } = useRequests()
  const { data: convs } = useConversations()
  const incoming = requests?.incoming.length ?? 0
  const unread = (convs ?? []).reduce((n, c) => n + c.unreadCount, 0)

  return (
    <div className={`app ${inChat ? 'in-chat' : ''}`}>
      <aside className="sidebar">
        <header className="sidebar-header">
          <button className="profile-btn" onClick={() => setShowProfile(true)} title="โปรไฟล์">
            <Avatar url={user?.avatarUrl} name={user?.displayName ?? '?'} size={36} />
            <span>{user?.displayName}</span>
          </button>
        </header>
        <nav className="tabs">
          <button className={tab === 'chats' ? 'active' : ''} onClick={() => setTab('chats')}>
            แชท{unread > 0 && <span className="badge">{unread > 99 ? '99+' : unread}</span>}
          </button>
          <button className={tab === 'friends' ? 'active' : ''} onClick={() => setTab('friends')}>เพื่อน</button>
          <button className={tab === 'requests' ? 'active' : ''} onClick={() => setTab('requests')}>
            คำขอ{incoming > 0 && <span className="badge">{incoming}</span>}
          </button>
        </nav>
        {tab === 'chats' && <ConversationList onCreateGroup={() => setShowGroup(true)} />}
        {tab === 'friends' && <FriendsPanel />}
        {tab === 'requests' && <RequestsPanel />}
      </aside>
      <main className="main">
        <Outlet />
      </main>
      {showGroup && <CreateGroupDialog onClose={() => setShowGroup(false)} />}
      {showProfile && <ProfileDialog onClose={() => setShowProfile(false)} />}
    </div>
  )
}

export function EmptyChat() {
  return (
    <div className="chat-empty">
      <div className="brand big">💬</div>
      <p>เลือกแชทเพื่อเริ่มสนทนา</p>
    </div>
  )
}

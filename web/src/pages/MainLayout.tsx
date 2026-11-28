import { useState, type ReactNode } from 'react'
import { Navigate, Outlet, useMatch } from 'react-router-dom'
import AppLock from '../components/AppLock'
import Avatar from '../components/Avatar'
import BrandLogo from '../components/BrandLogo'
import ConversationList from '../components/ConversationList'
import CreateGroupDialog from '../components/CreateGroupDialog'
import FriendsPanel from '../components/FriendsPanel'
import { ChatIcon, PlusIcon, SettingsIcon, UserPlusIcon, UsersIcon } from '../components/icons'
import ProfileDialog from '../components/ProfileDialog'
import RequestsPanel from '../components/RequestsPanel'
import SettingsDialog from '../components/SettingsDialog'
import { EmptyState } from '../components/Skeletons'
import ThemeToggle from '../components/ThemeToggle'
import { useConversations, useRequests } from '../hooks/queries'
import { useRealtime } from '../hubs/useRealtime'
import { useT, type TKey } from '../i18n'
import { useAppLock } from '../store/appLock'
import { useAuth } from '../store/authContext'

type Tab = 'chats' | 'friends' | 'requests'

const TAB_TITLE: Record<Tab, TKey> = { chats: 'tab.chats', friends: 'tab.friends', requests: 'tab.requests' }

export default function MainLayout() {
  const { t } = useT()
  const { status, user } = useAuth()
  if (status === 'loading') return <div className="splash"><span className="spinner" aria-label={t('common.loading')} /></div>
  if (status === 'anon' || !user) return <Navigate to="/login" replace />
  return <Shell meId={user.id} />
}

function RailButton({ active, label, badge, onClick, children }: { active?: boolean; label: string; badge?: number; onClick: () => void; children: ReactNode }) {
  return (
    <button className={`rail-btn${active ? ' active' : ''}`} onClick={onClick} aria-label={label} title={label} aria-current={active ? 'page' : undefined}>
      {children}
      {!!badge && badge > 0 && <span className="badge floating">{badge > 99 ? '99+' : badge}</span>}
      <span className="rail-label">{label}</span>
    </button>
  )
}

function Shell({ meId }: { meId: string }) {
  const { t } = useT()
  const { user } = useAuth()
  useRealtime(meId)
  const inChat = !!useMatch('/c/:id')
  // While the PIN lock is up, the UI behind it can't be focused or clicked.
  const { locked } = useAppLock(meId)
  const [tab, setTab] = useState<Tab>('chats')
  const [showGroup, setShowGroup] = useState(false)
  const [showProfile, setShowProfile] = useState(false)
  const [showSettings, setShowSettings] = useState(false)
  const { data: requests } = useRequests()
  const { data: convs } = useConversations()
  const incoming = requests?.incoming.length ?? 0
  const unread = (convs ?? []).reduce((n, c) => n + c.unreadCount, 0)

  return (
    <div className={`app ${inChat ? 'in-chat' : ''}`}>
      <nav className="rail" aria-label="Main" inert={locked}>
        <div className="rail-logo" aria-hidden="true"><BrandLogo /></div>
        <RailButton active={tab === 'chats'} label={t('tab.chats')} badge={unread} onClick={() => setTab('chats')}><ChatIcon /></RailButton>
        <RailButton active={tab === 'friends'} label={t('tab.friends')} onClick={() => setTab('friends')}><UsersIcon /></RailButton>
        <RailButton active={tab === 'requests'} label={t('tab.requests')} badge={incoming} onClick={() => setTab('requests')}><UserPlusIcon /></RailButton>
        <span className="rail-spacer" />
        <ThemeToggle className="rail-theme" />
        <RailButton active={showSettings} label={t('tab.settings')} onClick={() => setShowSettings(true)}><SettingsIcon /></RailButton>
        <button className="rail-profile" onClick={() => setShowProfile(true)} aria-label={t('profile.title')} title={user?.displayName}>
          <Avatar url={user?.avatarUrl} name={user?.displayName ?? '?'} size={36} />
        </button>
      </nav>

      <aside className="list-panel" inert={locked}>
        <header className="panel-head">
          <h1>{t(TAB_TITLE[tab])}</h1>
          <div className="panel-head-actions">
            {tab === 'chats' && (
              <button className="icon-btn filled" onClick={() => setShowGroup(true)} aria-label={t('chats.newGroup')} title={t('chats.newGroup')}><PlusIcon /></button>
            )}
            <ThemeToggle className="head-theme" />
          </div>
        </header>
        {tab === 'chats' && <ConversationList />}
        {tab === 'friends' && <FriendsPanel />}
        {tab === 'requests' && <RequestsPanel />}
      </aside>

      <main className="main" inert={locked}>
        <Outlet />
      </main>
      {!locked && showGroup && <CreateGroupDialog onClose={() => setShowGroup(false)} />}
      {!locked && showProfile && <ProfileDialog onClose={() => setShowProfile(false)} />}
      {!locked && showSettings && <SettingsDialog onClose={() => setShowSettings(false)} onOpenProfile={() => { setShowSettings(false); setShowProfile(true) }} />}
      <AppLock userId={meId} />
    </div>
  )
}

export function EmptyChat() {
  const { t } = useT()
  return (
    <div className="chat-empty">
      <EmptyState icon={<ChatIcon />} title={t('empty.pick')} hint={t('empty.pickHint')} />
    </div>
  )
}

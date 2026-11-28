import { useState, type ReactNode } from 'react'
import { APP_VERSION } from '../constants'
import { useT } from '../i18n'
import { useAuth, useMe } from '../store/authContext'
import { updateSettings, useSettings, type FontSize, type Language, type ThemeSetting } from '../store/settings'
import Avatar from './Avatar'
import { LogoutIcon } from './icons'
import Modal from './Modal'

function Segmented<T extends string>({ value, options, onChange, label }: {
  value: T
  options: { value: T; label: string }[]
  onChange: (v: T) => void
  label: string
}) {
  return (
    <div className="segmented" role="radiogroup" aria-label={label}>
      {options.map((o) => (
        <button key={o.value} role="radio" aria-checked={value === o.value} className={value === o.value ? 'active' : ''} onClick={() => onChange(o.value)}>
          {o.label}
        </button>
      ))}
    </div>
  )
}

function Switch({ checked, onChange, label }: { checked: boolean; onChange: (v: boolean) => void; label: string }) {
  return (
    <button role="switch" aria-checked={checked} aria-label={label} className={`switch${checked ? ' on' : ''}`} onClick={() => onChange(!checked)}>
      <span className="knob" />
    </button>
  )
}

function Row({ title, hint, children }: { title: string; hint?: string; children: ReactNode }) {
  return (
    <div className="setting-row">
      <div className="setting-text">
        <strong>{title}</strong>
        {hint && <span>{hint}</span>}
      </div>
      {children}
    </div>
  )
}

export default function SettingsDialog({ onClose, onOpenProfile }: { onClose: () => void; onOpenProfile: () => void }) {
  const { t } = useT()
  const me = useMe()
  const { logout } = useAuth()
  const s = useSettings()
  const [notifHint, setNotifHint] = useState<string | null>(null)

  const supported = typeof Notification !== 'undefined'

  const toggleDesktop = async (on: boolean) => {
    setNotifHint(null)
    if (!on) return updateSettings({ desktopNotifications: false })
    if (!supported) return setNotifHint(t('settings.notif.unsupported'))
    // Ask for permission only now, because the user turned the option on.
    const perm = Notification.permission === 'default' ? await Notification.requestPermission() : Notification.permission
    if (perm === 'granted') updateSettings({ desktopNotifications: true })
    else setNotifHint(t('settings.notif.denied'))
  }

  return (
    <Modal title={t('settings.title')} onClose={onClose} wide>
      <section className="settings-section">
        <h3>{t('settings.general')}</h3>
        <Row title={t('settings.language')}>
          <Segmented<Language> label={t('settings.language')} value={s.language} onChange={(language) => updateSettings({ language })}
            options={[{ value: 'th', label: 'ไทย' }, { value: 'en', label: 'English' }]} />
        </Row>
        <Row title={t('settings.theme')}>
          <Segmented<ThemeSetting> label={t('settings.theme')} value={s.theme} onChange={(theme) => updateSettings({ theme })}
            options={[{ value: 'light', label: t('settings.theme.light') }, { value: 'dark', label: t('settings.theme.dark') }, { value: 'system', label: t('settings.theme.system') }]} />
        </Row>
        <Row title={t('settings.fontSize')}>
          <Segmented<FontSize> label={t('settings.fontSize')} value={s.fontSize} onChange={(fontSize) => updateSettings({ fontSize })}
            options={[{ value: 'small', label: t('settings.font.small') }, { value: 'medium', label: t('settings.font.medium') }, { value: 'large', label: t('settings.font.large') }]} />
        </Row>
      </section>

      <section className="settings-section">
        <h3>{t('settings.messaging')}</h3>
        <Row title={t('settings.enterToSend')} hint={s.enterToSend ? t('settings.enterToSend.on') : t('settings.enterToSend.off')}>
          <Switch label={t('settings.enterToSend')} checked={s.enterToSend} onChange={(enterToSend) => updateSettings({ enterToSend })} />
        </Row>
      </section>

      <section className="settings-section">
        <h3>{t('settings.notifications')}</h3>
        <Row title={t('settings.sound')} hint={t('settings.sound.hint')}>
          <Switch label={t('settings.sound')} checked={s.sound} onChange={(sound) => updateSettings({ sound })} />
        </Row>
        <Row title={t('settings.desktop')} hint={t('settings.desktop.hint')}>
          <Switch label={t('settings.desktop')} checked={s.desktopNotifications} onChange={(on) => void toggleDesktop(on)} />
        </Row>
        {notifHint && <p className="error" role="alert">{notifHint}</p>}
      </section>

      <section className="settings-section">
        <h3>{t('settings.account')}</h3>
        <div className="account-card">
          <Avatar url={me.avatarUrl} name={me.displayName} size={44} />
          <div className="list-main">
            <span className="list-title">{me.displayName}</span>
            <span className="list-sub">@{me.username}</span>
          </div>
          <button className="btn small" onClick={onOpenProfile}>{t('settings.editProfile')}</button>
        </div>
        <button className="btn danger-outline block" onClick={() => void logout()}><LogoutIcon /> {t('settings.logout')}</button>
      </section>

      <section className="settings-section">
        <h3>{t('settings.about')}</h3>
        <Row title="KubChat"><span className="muted">{t('settings.version', { v: APP_VERSION })}</span></Row>
      </section>
    </Modal>
  )
}

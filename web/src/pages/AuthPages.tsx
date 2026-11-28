import { useState, type FormEvent, type ReactNode } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { errorMessage } from '../api/client'
import ThemeToggle from '../components/ThemeToggle'
import { useT } from '../i18n'
import { useAuth } from '../store/authContext'
import { updateSettings, useSettings } from '../store/settings'

const USERNAME_RE = /^[a-zA-Z0-9_]{3,20}$/
const PASSWORD_RE = /^(?=.*[A-Za-z])(?=.*\d).{8,}$/

function Shell({ title, children }: { title: string; children: ReactNode }) {
  const { language } = useSettings()
  return (
    <div className="auth-page">
      <div className="auth-tools">
        <div className="segmented small" role="group" aria-label="Language">
          <button className={language === 'th' ? 'active' : ''} onClick={() => updateSettings({ language: 'th' })}>TH</button>
          <button className={language === 'en' ? 'active' : ''} onClick={() => updateSettings({ language: 'en' })}>EN</button>
        </div>
        <ThemeToggle />
      </div>
      <div className="auth-card">
        <h2 className="brand"><img className="brand-logo" src="/kubchat-logo.webp" alt="KubChat" width={720} height={540} /></h2>
        <h1>{title}</h1>
        {children}
      </div>
    </div>
  )
}

export function LoginPage() {
  const { t } = useT()
  const { status, login } = useAuth()
  const navigate = useNavigate()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  if (status === 'authed') return <Navigate to="/" replace />

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      await login(username.trim(), password)
      navigate('/', { replace: true })
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Shell title={t('auth.login.title')}>
      <form onSubmit={submit} className="form">
        <label>{t('auth.username')}
          <input value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="username" autoFocus required />
        </label>
        <label>{t('auth.password')}
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" required />
        </label>
        {error && <p className="error" role="alert">{error}</p>}
        <button className="btn primary block" disabled={busy || !username || !password}>{busy ? t('auth.login.busy') : t('auth.login.submit')}</button>
      </form>
      <p className="auth-switch">{t('auth.noAccount')} <Link to="/register">{t('auth.register.link')}</Link></p>
    </Shell>
  )
}

export function RegisterPage() {
  const { t } = useT()
  const { status, register } = useAuth()
  const navigate = useNavigate()
  const [username, setUsername] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  if (status === 'authed') return <Navigate to="/" replace />

  const validation =
    !USERNAME_RE.test(username) ? t('auth.err.username')
    : displayName.trim().length < 1 || displayName.trim().length > 50 ? t('auth.err.displayName')
    : !PASSWORD_RE.test(password) ? t('auth.err.password')
    : null

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (validation) return setError(validation)
    setError(null)
    setBusy(true)
    try {
      await register(username, displayName.trim(), password)
      navigate('/', { replace: true })
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Shell title={t('auth.register.title')}>
      <form onSubmit={submit} className="form">
        <label>{t('auth.usernameFull')}
          <input value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="username" maxLength={20} autoFocus required />
        </label>
        <label>{t('auth.displayName')}
          <input value={displayName} onChange={(e) => setDisplayName(e.target.value)} maxLength={50} required />
        </label>
        <label>{t('auth.password')}
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="new-password" required />
        </label>
        <p className="hint">{t('auth.passwordHint')}</p>
        {error && <p className="error" role="alert">{error}</p>}
        <button className="btn primary block" disabled={busy}>{busy ? t('auth.register.busy') : t('auth.register.submit')}</button>
      </form>
      <p className="auth-switch">{t('auth.haveAccount')} <Link to="/login">{t('auth.login.link')}</Link></p>
    </Shell>
  )
}

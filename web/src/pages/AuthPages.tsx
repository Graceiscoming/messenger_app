import { useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { errorMessage } from '../api/client'
import { useAuth } from '../store/auth'

const USERNAME_RE = /^[a-zA-Z0-9_]{3,20}$/
const PASSWORD_RE = /^(?=.*[A-Za-z])(?=.*\d).{8,}$/

function Shell({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="auth-page">
      <div className="auth-card">
        <div className="brand">💬 Messenger</div>
        <h1>{title}</h1>
        {children}
      </div>
    </div>
  )
}

export function LoginPage() {
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
    <Shell title="เข้าสู่ระบบ">
      <form onSubmit={submit} className="form">
        <label>ชื่อผู้ใช้
          <input value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="username" autoFocus required />
        </label>
        <label>รหัสผ่าน
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" required />
        </label>
        {error && <p className="error" role="alert">{error}</p>}
        <button className="btn primary" disabled={busy || !username || !password}>{busy ? 'กำลังเข้าสู่ระบบ...' : 'เข้าสู่ระบบ'}</button>
      </form>
      <p className="auth-switch">ยังไม่มีบัญชี? <Link to="/register">สมัครสมาชิก</Link></p>
    </Shell>
  )
}

export function RegisterPage() {
  const { status, register } = useAuth()
  const navigate = useNavigate()
  const [username, setUsername] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  if (status === 'authed') return <Navigate to="/" replace />

  const validation =
    !USERNAME_RE.test(username) ? 'ชื่อผู้ใช้ต้องเป็น a-z, 0-9, _ ความยาว 3-20 ตัว'
    : displayName.trim().length < 1 || displayName.trim().length > 50 ? 'ชื่อที่แสดงต้องยาว 1-50 ตัวอักษร'
    : !PASSWORD_RE.test(password) ? 'รหัสผ่านต้องยาวอย่างน้อย 8 ตัว และมีทั้งตัวอักษรและตัวเลข'
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
    <Shell title="สมัครสมาชิก">
      <form onSubmit={submit} className="form">
        <label>ชื่อผู้ใช้ (username)
          <input value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="username" maxLength={20} autoFocus required />
        </label>
        <label>ชื่อที่แสดง
          <input value={displayName} onChange={(e) => setDisplayName(e.target.value)} maxLength={50} required />
        </label>
        <label>รหัสผ่าน
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="new-password" required />
        </label>
        <p className="hint">รหัสผ่านอย่างน้อย 8 ตัว มีตัวอักษรและตัวเลข</p>
        {error && <p className="error" role="alert">{error}</p>}
        <button className="btn primary" disabled={busy}>{busy ? 'กำลังสมัคร...' : 'สมัครสมาชิก'}</button>
      </form>
      <p className="auth-switch">มีบัญชีแล้ว? <Link to="/login">เข้าสู่ระบบ</Link></p>
    </Shell>
  )
}

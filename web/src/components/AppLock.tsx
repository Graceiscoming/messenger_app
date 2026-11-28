import { useCallback, useEffect, useState } from 'react'
import { useT } from '../i18n'
import { clearPin, cooldownLeft, lockApp, RELOCK_AFTER_MS, unlockApp, useAppLock, verifyPin } from '../store/appLock'
import { useAuth } from '../store/authContext'
import { LockIcon } from './icons'
import PinPad from './PinPad'

/**
 * Full-screen PIN lock over the chat UI. Shown on every page load when a PIN is set for this user,
 * and again after the tab has been in the background for RELOCK_AFTER_MS.
 */
export default function AppLock({ userId }: { userId: string }) {
  const { t } = useT()
  const { logout } = useAuth()
  const { locked, enabled } = useAppLock(userId)
  const [error, setError] = useState<string | null>(null)
  const [wait, setWait] = useState(() => cooldownLeft(userId))

  // Drop focus from whatever was focused behind the lock (e.g. the message box).
  useEffect(() => {
    if (locked && document.activeElement instanceof HTMLElement) document.activeElement.blur()
  }, [locked])

  // Re-lock after being hidden for a while.
  useEffect(() => {
    if (!enabled) return
    let hiddenAt = 0
    const onVis = () => {
      if (document.visibilityState === 'hidden') hiddenAt = Date.now()
      else if (hiddenAt && Date.now() - hiddenAt >= RELOCK_AFTER_MS) lockApp()
    }
    document.addEventListener('visibilitychange', onVis)
    return () => document.removeEventListener('visibilitychange', onVis)
  }, [enabled])

  // Count down the cooldown after too many wrong tries.
  useEffect(() => {
    if (wait <= 0) return
    const id = window.setInterval(() => setWait(cooldownLeft(userId)), 500)
    return () => window.clearInterval(id)
  }, [wait, userId])

  const onComplete = useCallback(async (pin: string) => {
    const r = await verifyPin(userId, pin)
    if (r.ok) { setError(null); unlockApp(); return true }
    if (r.cooldownMs > 0) { setWait(r.cooldownMs); setError(t('pin.tooMany')) }
    else setError(t('pin.wrong', { n: r.attemptsLeft }))
    return false
  }, [userId, t])

  if (!locked) return null
  const seconds = Math.ceil(wait / 1000)

  return (
    <div className="app-lock" role="dialog" aria-modal="true" aria-label={t('pin.lockTitle')}>
      <div className="app-lock-card">
        <img className="app-lock-logo" src="/kubchat-icon.png" alt="" width={256} height={256} draggable={false} />
        <h2><LockIcon /> {t('pin.lockTitle')}</h2>
        <p className="muted">{wait > 0 ? t('pin.wait', { s: seconds }) : t('pin.lockHint')}</p>
        <PinPad onComplete={onComplete} error={wait > 0 ? null : error} disabled={wait > 0} />
        {/* Forgot PIN: drop it and sign out; signing in again with the password proves the user. */}
        <button className="link" onClick={() => { clearPin(userId); void logout() }}>{t('pin.forgot')}</button>
      </div>
    </div>
  )
}

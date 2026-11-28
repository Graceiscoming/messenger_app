import { useCallback, useEffect, useState } from 'react'
import { useT } from '../i18n'
import { PIN_LENGTH } from '../store/appLock'
import { BackspaceIcon } from './icons'

interface Props {
  /** Called once all digits are entered. Return false to shake and clear (wrong PIN). */
  onComplete: (pin: string) => Promise<boolean | void> | boolean | void
  error?: string | null
  disabled?: boolean
}

const KEYS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '', '0', 'del'] as const

/** 6 dots + numeric keypad. Also accepts the physical keyboard (digits, Backspace). Give it a new `key` to clear it. */
export default function PinPad({ onComplete, error, disabled }: Props) {
  const { t } = useT()
  const [pin, setPin] = useState('')
  const [busy, setBusy] = useState(false)
  const [shake, setShake] = useState(0)

  const submit = useCallback(async (full: string) => {
    setBusy(true)
    try {
      const ok = await onComplete(full)
      if (ok === false) { setShake((n) => n + 1); setPin('') }
    } finally { setBusy(false) }
  }, [onComplete])

  const press = useCallback((k: string) => {
    if (disabled || busy) return
    if (k === 'del') return setPin((p) => p.slice(0, -1))
    if (pin.length >= PIN_LENGTH) return
    const next = pin + k
    setPin(next)
    if (next.length === PIN_LENGTH) void submit(next)
  }, [disabled, busy, pin, submit])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.ctrlKey || e.metaKey || e.altKey) return
      if (/^\d$/.test(e.key)) { e.preventDefault(); press(e.key) }
      else if (e.key === 'Backspace') { e.preventDefault(); press('del') }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [press])

  return (
    <div className="pinpad">
      <div key={shake} className={`pin-dots${shake ? ' shake' : ''}`} role="status" aria-label={t('pin.entered', { n: pin.length, total: PIN_LENGTH })}>
        {Array.from({ length: PIN_LENGTH }, (_, i) => <span key={i} className={i < pin.length ? 'filled' : ''} />)}
      </div>
      <p className="pin-error" role="alert">{error ?? ''}</p>
      <div className="pin-keys">
        {KEYS.map((k, i) => k === '' ? <span key={i} /> : (
          <button
            key={i} type="button" className={`pin-key${k === 'del' ? ' del' : ''}`} disabled={disabled || busy}
            aria-label={k === 'del' ? t('pin.delete') : k} onClick={() => press(k)}
          >
            {k === 'del' ? <BackspaceIcon /> : k}
          </button>
        ))}
      </div>
    </div>
  )
}

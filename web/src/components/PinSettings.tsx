import { useState } from 'react'
import { useT, type TKey } from '../i18n'
import { clearPin, pinSupported, setPin, useAppLock, verifyPin } from '../store/appLock'
import PinPad from './PinPad'
import { Row, Switch } from './SettingControls'

type Mode = 'enable' | 'change' | 'disable'
type Step = 'current' | 'new' | 'confirm'
interface Flow { mode: Mode; step: Step; first?: string }

const STEP_TITLE: Record<Step, TKey> = { current: 'pin.step.current', new: 'pin.step.new', confirm: 'pin.step.confirm' }

/** "Security" section of Settings: turn the PIN lock on/off and change the PIN (inline keypad, no nested modal). */
export default function PinSettings({ userId }: { userId: string }) {
  const { t } = useT()
  const { enabled } = useAppLock(userId)
  const [flow, setFlow] = useState<Flow | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState<string | null>(null)

  const start = (mode: Mode) => {
    setError(null); setDone(null)
    setFlow({ mode, step: mode === 'enable' ? 'new' : 'current' })
  }
  const finish = (msg: TKey) => { setFlow(null); setError(null); setDone(t(msg)) }

  const onComplete = async (pin: string) => {
    if (!flow) return
    if (flow.step === 'current') {
      const r = await verifyPin(userId, pin)
      if (!r.ok) {
        setError(r.cooldownMs > 0 ? t('pin.wait', { s: Math.ceil(r.cooldownMs / 1000) }) : t('pin.wrong', { n: r.attemptsLeft }))
        return false
      }
      setError(null)
      if (flow.mode === 'disable') { clearPin(userId); return finish('pin.disabled') }
      return setFlow({ mode: flow.mode, step: 'new' })
    }
    if (flow.step === 'new') { setError(null); return setFlow({ ...flow, step: 'confirm', first: pin }) }
    // confirm
    if (pin !== flow.first) { setFlow({ mode: flow.mode, step: 'new' }); setError(t('pin.mismatch')); return false }
    await setPin(userId, pin)
    finish('pin.saved')
  }

  return (
    <section className="settings-section">
      <h3>{t('settings.security')}</h3>
      <Row title={t('pin.setting')} hint={pinSupported ? t('pin.setting.hint') : t('pin.unsupported')}>
        <Switch
          label={t('pin.setting')} checked={enabled || flow?.mode === 'enable'}
          onChange={(on) => {
            if (!pinSupported) return
            if (flow) return setFlow(null)
            start(on ? 'enable' : 'disable')
          }}
        />
      </Row>
      {enabled && !flow && (
        <button className="btn small" onClick={() => start('change')}>{t('pin.change')}</button>
      )}
      {done && !flow && <p className="muted" role="status">{done}</p>}
      {flow && (
        <div className="pin-setup">
          <strong>{t(STEP_TITLE[flow.step])}</strong>
          <PinPad key={`${flow.mode}-${flow.step}`} onComplete={onComplete} error={error} />
          <button className="btn small" onClick={() => { setFlow(null); setError(null) }}>{t('common.cancel')}</button>
        </div>
      )}
    </section>
  )
}

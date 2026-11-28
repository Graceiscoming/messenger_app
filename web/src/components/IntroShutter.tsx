import { useEffect, useState, type CSSProperties } from 'react'
import { useT } from '../i18n'

const SEEN_KEY = 'kubchat.introSeen'
const LIFT_AT = 1900 // ms: logo + loading line play, then the shutters rise
const DONE_AT = 3100 // ms: last shutter has left the screen

/** Rising sparkles: fixed pseudo-random spread so the layout is stable between renders. */
const SPARKS = Array.from({ length: 22 }, (_, i) => ({
  '--x': `${(i * 37 + 11) % 100}%`,
  '--s': `${3 + (i * 7) % 5}px`,
  '--d': `${2.6 + ((i * 13) % 10) / 5}s`,
  '--delay': `${-((i * 0.29) % 2.6).toFixed(2)}s`,
  '--drift': `${((i * 23) % 60) - 30}px`,
}) as CSSProperties)

function shouldShow() {
  try {
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return false
    return sessionStorage.getItem(SEEN_KEY) !== '1'
  } catch {
    return true
  }
}

/**
 * Opening splash: a theme-coloured (light/dark) "shutter" with blue-pink accents and the KubChat cat that slides up in three layers to reveal the app.
 * Shown once per browser tab session; click / any key skips it. Skipped entirely for reduced-motion users.
 */
export default function IntroShutter() {
  const { t } = useT()
  const [phase, setPhase] = useState<'show' | 'lift' | 'done'>(() => (shouldShow() ? 'show' : 'done'))

  useEffect(() => {
    if (phase === 'done') return
    try { sessionStorage.setItem(SEEN_KEY, '1') } catch { /* storage blocked: intro just replays */ }
    const lift = window.setTimeout(() => setPhase('lift'), LIFT_AT)
    const done = window.setTimeout(() => setPhase('done'), DONE_AT)
    return () => { window.clearTimeout(lift); window.clearTimeout(done) }
    // Timers are scheduled once on mount; skip() shortens them.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    if (phase !== 'lift') return
    const id = window.setTimeout(() => setPhase('done'), DONE_AT - LIFT_AT)
    return () => window.clearTimeout(id)
  }, [phase])

  useEffect(() => {
    if (phase !== 'show') return
    const skip = () => setPhase('lift')
    window.addEventListener('keydown', skip)
    return () => window.removeEventListener('keydown', skip)
  }, [phase])

  if (phase === 'done') return null

  return (
    <div className={`intro ${phase === 'lift' ? 'lift' : ''}`} aria-hidden="true" onClick={() => setPhase('lift')}>
      <div className="intro-layer l3" />
      <div className="intro-layer l2" />
      <div className="intro-layer l1">
        <div className="intro-glow" />
        <div className="intro-sparks">{SPARKS.map((st, i) => <i key={i} style={st} />)}</div>
        <div className="intro-sheen" />
        <div className="intro-content">
          <div className="intro-badge">
            <span className="intro-ring" />
            <span className="intro-ring r2" />
            <div className="intro-logo-wrap">
              <img className="intro-logo" src="/kubchat-icon.png" alt="" width={256} height={256} draggable={false} />
            </div>
          </div>
          <h1 className="intro-title">
            {'KubChat'.split('').map((c, i) => <span key={i} style={{ animationDelay: `${0.45 + i * 0.05}s`, '--p': `${Math.round((i / 6) * 100)}%` } as CSSProperties}>{c}</span>)}
          </h1>
          <p className="intro-tagline">{t('intro.tagline')}</p>
          <div className="intro-bar"><span /></div>
        </div>
        <span className="intro-skip">{t('intro.skip')}</span>
        <div className="intro-edge" />
      </div>
    </div>
  )
}

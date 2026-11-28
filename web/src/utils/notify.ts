import type { MessageDto } from '../types/api'
import { getSettings } from '../store/settings'
import { messagePreview } from './format'

let ctx: AudioContext | null = null

/** Short two-tone beep via WebAudio (no audio asset needed). */
export function playBeep() {
  try {
    ctx ??= new AudioContext()
    if (ctx.state === 'suspended') void ctx.resume()
    const now = ctx.currentTime
    ;[880, 1320].forEach((freq, i) => {
      const osc = ctx!.createOscillator()
      const gain = ctx!.createGain()
      const t0 = now + i * 0.11
      osc.frequency.value = freq
      gain.gain.setValueAtTime(0.0001, t0)
      gain.gain.exponentialRampToValueAtTime(0.12, t0 + 0.02)
      gain.gain.exponentialRampToValueAtTime(0.0001, t0 + 0.1)
      osc.connect(gain).connect(ctx!.destination)
      osc.start(t0)
      osc.stop(t0 + 0.12)
    })
  } catch { /* audio unavailable */ }
}

/** For an incoming message from someone else that the user is not currently looking at. */
export function notifyIncoming(msg: MessageDto, meId: string) {
  const s = getSettings()
  if (s.sound) playBeep()
  if (s.desktopNotifications && document.visibilityState === 'hidden'
    && typeof Notification !== 'undefined' && Notification.permission === 'granted') {
    try {
      new Notification(msg.sender.displayName, { body: messagePreview(msg, meId), tag: msg.conversationId })
    } catch { /* ignore */ }
  }
}

import { useAuthedImage } from '../hooks/useAuthedImage'
import { useT } from '../i18n'
import { UsersIcon } from './icons'

// Avatar fallback hues; saturation/lightness are chosen so white initials keep contrast.
const HUES = [152, 200, 24, 262, 330, 174, 38]

function hueFor(name: string) {
  let h = 0
  for (const ch of name) h = (h * 31 + ch.codePointAt(0)!) >>> 0
  return HUES[h % HUES.length]
}

interface Props {
  url?: string | null
  name: string
  size?: number
  online?: boolean
  group?: boolean
}

export default function Avatar({ url, name, size = 44, online, group }: Props) {
  const { t } = useT()
  const src = useAuthedImage(url)
  const initial = (Array.from(name.trim())[0] ?? '?').toUpperCase()
  return (
    <span className="avatar" style={{ width: size, height: size, fontSize: size * 0.4 }}>
      {src ? (
        <img src={src} alt="" />
      ) : (
        <span className="avatar-fallback" style={{ ['--hue' as string]: hueFor(name) }}>
          {group ? <UsersIcon /> : initial}
        </span>
      )}
      {online !== undefined && (
        <span className={`dot ${online ? 'online' : 'offline'}`} title={online ? t('common.online') : t('common.offline')} />
      )}
    </span>
  )
}

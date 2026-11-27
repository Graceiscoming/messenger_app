import { useAuthedImage } from '../hooks/useAuthedImage'

const COLORS = ['#06c755', '#4aa3ff', '#ff8a5c', '#a78bfa', '#f472b6', '#14b8a6', '#eab308']

function colorFor(name: string) {
  let h = 0
  for (const ch of name) h = (h * 31 + ch.codePointAt(0)!) >>> 0
  return COLORS[h % COLORS.length]
}

interface Props {
  url?: string | null
  name: string
  size?: number
  online?: boolean
  group?: boolean
}

export default function Avatar({ url, name, size = 44, online, group }: Props) {
  const src = useAuthedImage(url)
  const initial = group ? '👥' : (Array.from(name.trim())[0] ?? '?').toUpperCase()
  return (
    <span className="avatar" style={{ width: size, height: size, fontSize: size * 0.42 }}>
      {src ? (
        <img src={src} alt="" />
      ) : (
        <span className="avatar-fallback" style={{ background: colorFor(name) }}>{initial}</span>
      )}
      {online !== undefined && <span className={`dot ${online ? 'online' : 'offline'}`} title={online ? 'ออนไลน์' : 'ออฟไลน์'} />}
    </span>
  )
}

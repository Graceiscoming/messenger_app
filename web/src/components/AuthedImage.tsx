import { useAuthedImage } from '../hooks/useAuthedImage'

interface Props {
  url: string
  alt?: string
  className?: string
  onLoad?: () => void
  onClick?: (src: string) => void
}

/** <img> for protected files: fetches with the Bearer token and shows a placeholder meanwhile. */
export default function AuthedImage({ url, alt = '', className, onLoad, onClick }: Props) {
  const src = useAuthedImage(url)
  if (!src) return <span className={`img-placeholder ${className ?? ''}`} aria-label="กำลังโหลดรูป" />
  return <img className={className} src={src} alt={alt} onLoad={onLoad} onClick={onClick ? () => onClick(src) : undefined} />
}

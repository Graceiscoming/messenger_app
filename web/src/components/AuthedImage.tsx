import { useAuthedImage } from '../hooks/useAuthedImage'
import { useT } from '../i18n'

interface Props {
  url: string
  alt?: string
  className?: string
  onLoad?: () => void
  onClick?: (src: string) => void
}

/** <img> for protected files: fetches with the Bearer token and shows a skeleton meanwhile. */
export default function AuthedImage({ url, alt = '', className, onLoad, onClick }: Props) {
  const { t } = useT()
  const src = useAuthedImage(url)
  if (!src) return <span className={`img-placeholder skeleton ${className ?? ''}`} aria-label={t('img.loading')} />
  return <img className={className} src={src} alt={alt} onLoad={onLoad} onClick={onClick ? () => onClick(src) : undefined} />
}

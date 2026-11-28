import { useMemo, useState } from 'react'
import { useStickers } from '../hooks/queries'
import { useT, type TKey } from '../i18n'
import type { StickerDto } from '../types/api'
import { SearchIcon } from './icons'

const PACK_LABELS: Record<string, TKey> = {
  smileys: 'stickers.pack.smileys',
  faces: 'stickers.pack.faces',
  feelings: 'stickers.pack.feelings',
  monsters: 'stickers.pack.monsters',
  animals: 'stickers.pack.animals',
  hearts: 'stickers.pack.hearts',
}

/** "face-with-tears-of-joy" -> "face with tears of joy" (sticker codes are English CLDR names). */
const label = (code: string) => code.replace(/-/g, ' ')

export default function StickerPicker({ onPick }: { onPick: (s: StickerDto) => void }) {
  const { t } = useT()
  const { data: packs, isLoading, isError } = useStickers()
  const [packId, setPackId] = useState<number | null>(null)
  const [q, setQ] = useState('')
  const pack = packs?.find((p) => p.id === packId) ?? packs?.[0]
  const packLabel = (name: string) => (PACK_LABELS[name] ? t(PACK_LABELS[name]) : name)

  // A search looks across every pack; otherwise show the selected pack.
  const query = q.trim().toLowerCase()
  const results = useMemo(() => {
    if (!query || !packs) return null
    const words = query.split(/\s+/)
    return packs.flatMap((p) => p.stickers).filter((s) => words.every((w) => label(s.code).includes(w)))
  }, [packs, query])
  const shown = results ?? pack?.stickers ?? []

  return (
    <div className="sticker-picker">
      <div className="sticker-search">
        <label className="search-box">
          <SearchIcon />
          <input
            placeholder={t('stickers.search')} aria-label={t('stickers.search')}
            value={q} onChange={(e) => setQ(e.target.value)}
          />
        </label>
      </div>
      {isLoading && <div className="sticker-grid" aria-hidden="true">{Array.from({ length: 12 }, (_, i) => <span key={i} className="skeleton sk-sticker" />)}</div>}
      {isError && <p className="empty error">{t('stickers.loadError')}</p>}
      {pack && (
        <>
          {!results && <div className="sticker-pack-title">{packLabel(pack.name)}</div>}
          {results && results.length === 0
            ? <p className="empty">{t('stickers.noResults')}</p>
            : (
              <div className="sticker-grid">
                {shown.map((s) => (
                  <button key={s.id} className="sticker-btn" title={label(s.code)} onClick={() => onPick(s)}>
                    <img src={s.imageUrl} alt={label(s.code)} loading="lazy" />
                  </button>
                ))}
              </div>
            )}
          {packs && packs.length > 1 && (
            <div className="sticker-tabs" role="tablist">
              {packs.map((p) => (
                <button
                  key={p.id} role="tab" aria-selected={!results && p.id === pack.id}
                  className={!results && p.id === pack.id ? 'active' : ''}
                  title={packLabel(p.name)} aria-label={packLabel(p.name)}
                  onClick={() => { setPackId(p.id); setQ('') }}
                >
                  {p.thumbnailUrl ? <img src={p.thumbnailUrl} alt="" /> : packLabel(p.name)}
                </button>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  )
}

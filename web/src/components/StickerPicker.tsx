import { useState } from 'react'
import { useStickers } from '../hooks/queries'
import type { StickerDto } from '../types/api'

export default function StickerPicker({ onPick }: { onPick: (s: StickerDto) => void }) {
  const { data: packs, isLoading, isError } = useStickers()
  const [packId, setPackId] = useState<number | null>(null)
  const pack = packs?.find((p) => p.id === packId) ?? packs?.[0]

  return (
    <div className="sticker-picker">
      {isLoading && <p className="empty">กำลังโหลด...</p>}
      {isError && <p className="empty error">โหลดสติกเกอร์ไม่สำเร็จ</p>}
      {pack && (
        <>
          <div className="sticker-grid">
            {pack.stickers.map((s) => (
              <button key={s.id} className="sticker-btn" title={s.code} onClick={() => onPick(s)}>
                <img src={s.imageUrl} alt={s.code} loading="lazy" />
              </button>
            ))}
          </div>
          {packs && packs.length > 1 && (
            <div className="sticker-tabs">
              {packs.map((p) => (
                <button key={p.id} className={p.id === pack.id ? 'active' : ''} onClick={() => setPackId(p.id)}>
                  {p.thumbnailUrl ? <img src={p.thumbnailUrl} alt={p.name} /> : p.name}
                </button>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  )
}

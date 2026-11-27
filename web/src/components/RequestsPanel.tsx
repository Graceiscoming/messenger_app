import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { errorMessage } from '../api/client'
import { friendsApi } from '../api/endpoints'
import { keys, setRequests, useRequests } from '../hooks/queries'
import Avatar from './Avatar'

export default function RequestsPanel() {
  const qc = useQueryClient()
  const { data, isLoading } = useRequests()
  const [error, setError] = useState<string | null>(null)

  const run = async (id: string, fn: () => Promise<void>, refreshFriends = false) => {
    setError(null)
    try {
      await fn()
      setRequests(qc, (r) => ({ incoming: r.incoming.filter((x) => x.id !== id), outgoing: r.outgoing.filter((x) => x.id !== id) }))
      if (refreshFriends) void qc.invalidateQueries({ queryKey: keys.friends })
    } catch (err) {
      setError(errorMessage(err))
    }
  }

  return (
    <div className="panel">
      {error && <p className="error pad" role="alert">{error}</p>}
      <h3 className="section-title">คำขอที่ได้รับ</h3>
      <ul className="list">
        {isLoading && <li className="empty">กำลังโหลด...</li>}
        {!isLoading && !data?.incoming.length && <li className="empty">ไม่มีคำขอเป็นเพื่อน</li>}
        {data?.incoming.map((r) => (
          <li key={r.id} className="list-item static">
            <Avatar url={r.requester.avatarUrl} name={r.requester.displayName} />
            <div className="list-main">
              <span className="list-title">{r.requester.displayName}</span>
              <span className="list-sub">@{r.requester.username}</span>
            </div>
            <div className="row-actions">
              <button className="btn small primary" onClick={() => run(r.id, () => friendsApi.accept(r.id), true)}>ยอมรับ</button>
              <button className="btn small" onClick={() => run(r.id, () => friendsApi.decline(r.id))}>ปฏิเสธ</button>
            </div>
          </li>
        ))}
      </ul>
      <h3 className="section-title">คำขอที่ส่งไป</h3>
      <ul className="list">
        {!isLoading && !data?.outgoing.length && <li className="empty">ไม่มีคำขอที่รอการตอบรับ</li>}
        {data?.outgoing.map((r) => (
          <li key={r.id} className="list-item static">
            <Avatar url={r.addressee.avatarUrl} name={r.addressee.displayName} />
            <div className="list-main">
              <span className="list-title">{r.addressee.displayName}</span>
              <span className="list-sub">@{r.addressee.username} · รอการตอบรับ</span>
            </div>
            <button className="btn small" onClick={() => run(r.id, () => friendsApi.cancel(r.id))}>ยกเลิก</button>
          </li>
        ))}
      </ul>
    </div>
  )
}

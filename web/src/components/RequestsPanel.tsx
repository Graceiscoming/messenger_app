import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { errorMessage } from '../api/client'
import { friendsApi } from '../api/endpoints'
import { keys, setRequests, useRequests } from '../hooks/queries'
import { useT } from '../i18n'
import AddFriendSearch from './AddFriendSearch'
import Avatar from './Avatar'
import { UserPlusIcon } from './icons'
import { EmptyState, ListSkeleton } from './Skeletons'

export default function RequestsPanel() {
  const { t } = useT()
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

  const none = !isLoading && !data?.incoming.length && !data?.outgoing.length

  return (
    <div className="panel">
      <AddFriendSearch />
      {error && <p className="error pad" role="alert">{error}</p>}
      {isLoading && <ListSkeleton rows={3} />}
      {none && <EmptyState icon={<UserPlusIcon />} title={t('requests.none')} hint={t('requests.noneHint')} />}
      {!!data?.incoming.length && (
        <>
          <h3 className="section-title">{t('requests.incoming')}</h3>
          <ul className="list">
            {data.incoming.map((r) => (
              <li key={r.id} className="list-item static">
                <Avatar url={r.requester.avatarUrl} name={r.requester.displayName} />
                <div className="list-main">
                  <span className="list-title">{r.requester.displayName}</span>
                  <span className="list-sub">@{r.requester.username}</span>
                </div>
                <div className="row-actions">
                  <button className="btn small primary" onClick={() => run(r.id, () => friendsApi.accept(r.id), true)}>{t('requests.accept')}</button>
                  <button className="btn small" onClick={() => run(r.id, () => friendsApi.decline(r.id))}>{t('requests.decline')}</button>
                </div>
              </li>
            ))}
          </ul>
        </>
      )}
      {!!data?.outgoing.length && (
        <>
          <h3 className="section-title">{t('requests.outgoing')}</h3>
          <ul className="list">
            {data.outgoing.map((r) => (
              <li key={r.id} className="list-item static">
                <Avatar url={r.addressee.avatarUrl} name={r.addressee.displayName} />
                <div className="list-main">
                  <span className="list-title">{r.addressee.displayName}</span>
                  <span className="list-sub">@{r.addressee.username} · {t('requests.waiting')}</span>
                </div>
                <button className="btn small" onClick={() => run(r.id, () => friendsApi.cancel(r.id))}>{t('requests.cancel')}</button>
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  )
}

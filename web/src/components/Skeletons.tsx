import type { ReactNode } from 'react'

/** Placeholder rows shown while a list loads (replaces plain "loading" text). */
export function ListSkeleton({ rows = 6 }: { rows?: number }) {
  return (
    <ul className="list" aria-hidden="true">
      {Array.from({ length: rows }, (_, i) => (
        <li key={i} className="list-item static">
          <span className="skeleton circle" />
          <span className="list-main">
            <span className="skeleton line" style={{ width: `${55 + ((i * 17) % 30)}%` }} />
            <span className="skeleton line short" />
          </span>
        </li>
      ))}
    </ul>
  )
}

export function MessagesSkeleton() {
  return (
    <div className="msg-skeleton" aria-hidden="true">
      {[0, 1, 0, 0, 1].map((mine, i) => (
        <span key={i} className={`skeleton bubble-sk ${mine ? 'mine' : ''}`} style={{ width: `${30 + ((i * 23) % 35)}%` }} />
      ))}
    </div>
  )
}

export function EmptyState({ icon, title, hint, action }: { icon: ReactNode; title: string; hint?: string; action?: ReactNode }) {
  return (
    <div className="empty-state">
      <span className="empty-icon">{icon}</span>
      <strong>{title}</strong>
      {hint && <p>{hint}</p>}
      {action}
    </div>
  )
}

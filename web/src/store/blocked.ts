import { useCallback, useState } from 'react'
import type { UserDto } from '../types/api'

// The API has no "list blocked users" endpoint, so blocked users are remembered locally (per account)
// to let the user unblock them later. See docs/decisions.md.
const key = (me: string) => `messenger.blocked.${me}`

function load(me: string): UserDto[] {
  try { return JSON.parse(localStorage.getItem(key(me)) ?? '[]') as UserDto[] } catch { return [] }
}

export function useBlocked(me: string) {
  const [list, setList] = useState<UserDto[]>(() => load(me))
  const save = useCallback((next: UserDto[]) => {
    setList(next)
    try { localStorage.setItem(key(me), JSON.stringify(next)) } catch { /* ignore */ }
  }, [me])
  return {
    list,
    add: (u: UserDto) => save([...list.filter((x) => x.id !== u.id), u]),
    remove: (id: string) => save(list.filter((x) => x.id !== id)),
  }
}

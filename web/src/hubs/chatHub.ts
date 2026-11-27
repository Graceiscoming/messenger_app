import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { refreshSession, tokens } from '../api/client'

let current: HubConnection | null = null

const backoff = (n: number) => Math.min(30_000, 1000 * 2 ** n)

/** Build a hub connection that always uses a fresh access token and reconnects with exponential backoff. */
export function createHub(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl('/hubs/chat', {
      accessTokenFactory: async () => {
        if (tokens.isStale()) await refreshSession()
        return tokens.access ?? ''
      },
    })
    .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (ctx) => backoff(ctx.previousRetryCount) })
    .configureLogging(LogLevel.Warning)
    .build()
}

export function setCurrentHub(h: HubConnection | null) { current = h }

/** Fire-and-forget "typing" signal; silently ignored when disconnected. */
export function sendTyping(conversationId: string) {
  if (current?.state === HubConnectionState.Connected) {
    current.invoke('Typing', conversationId).catch(() => undefined)
  }
}

/** Initial connect with retry (automatic reconnect only covers drops after a successful start). */
export async function startWithRetry(h: HubConnection, isCancelled: () => boolean) {
  for (let attempt = 0; !isCancelled(); attempt++) {
    try {
      await h.start()
      return
    } catch {
      if (isCancelled()) return
      await new Promise((r) => setTimeout(r, backoff(attempt)))
    }
  }
}

// TODO(D): SignalR automatic reconnect + join group + resync snapshot.
import { useEffect, useState } from 'react'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { accessToken } from '../api/httpClient'
export function useDashboardHub(groupId: number, refresh: () => Promise<void>) {
  const [state, setState] = useState('Chưa kết nối')
  useEffect(() => {
    if (!groupId) return
    let disposed = false
    let timer: ReturnType<typeof setTimeout> | undefined
    const hub = new HubConnectionBuilder().withUrl('/hubs/revenue', { accessTokenFactory: accessToken }).withAutomaticReconnect().configureLogging(LogLevel.Warning).build()
    const sync = async () => { await hub.invoke('JoinGroup', groupId); await refresh(); if (!disposed) setState('Đã kết nối') }
    hub.on('RevenueChanged', () => { void refresh() })
    hub.on('ResyncRequired', () => { void refresh() })
    hub.onreconnecting(() => setState('Đang kết nối lại'))
    hub.onreconnected(() => { void sync().catch(() => setState('Không đồng bộ được')) })
    const start = async () => { try { await hub.start(); if (!disposed) await sync() } catch { if (!disposed) { setState('Đang chờ kết nối'); timer = setTimeout(() => { void start() }, 5000) } } }
    hub.onclose(() => { if (!disposed) timer = setTimeout(() => { void start() }, 5000) })
    void start()
    return () => { disposed = true; clearTimeout(timer); void hub.stop() }
  }, [groupId, refresh])
  return state
}

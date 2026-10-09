import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { writeFile, mkdir } from 'node:fs/promises'
import assert from 'node:assert/strict'

const base = process.env.REVENUEFLOW_TEST_API ?? 'http://localhost:5080'
const root = new URL('../../docs/evidence/', import.meta.url)
async function api(path, token, method = 'GET', body) {
  const r = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: body ? JSON.stringify(body) : undefined })
  if (!r.ok) throw new Error(`${method} ${path} returned ${r.status}: ${await r.text()}`)
  return r.status === 204 ? null : r.json()
}
async function login(i) { return (await api('/api/auth/login',null,'POST',{email:`demo${i}@revenueflow.local`,password:'Demo@123456'})).accessToken }
const tokens = await Promise.all([1,2,3].map(login))
const contexts = await Promise.all(tokens.map(t => api('/api/context',t)))
const group = contexts[0].groups[0].groupId
const counts = [0,0,0]
const hubs = tokens.map((token,i) => {
  const hub = new HubConnectionBuilder().withUrl(base + '/hubs/revenue',{ accessTokenFactory: () => token }).withAutomaticReconnect().configureLogging(LogLevel.Error).build()
  hub.on('RevenueChanged', () => { counts[i]++ })
  return hub
})
const wait = ms => new Promise(r => setTimeout(r,ms))
let id
try {
  await Promise.all(hubs.map(h => h.start()))
  await hubs[0].invoke('JoinGroup',group); await hubs[1].invoke('JoinGroup',group)
  await hubs[2].invoke('JoinGroup',contexts[2].groups[0].groupId)
  await assert.rejects(hubs[2].invoke('JoinGroup',group))
  const date = new Date().toISOString().slice(0,10)
  id = (await api('/api/revenues',tokens[0],'POST',{ walletId: contexts[0].wallets[0].id, amount: 9876, occurredOn: date, note:'Realtime integration smoke' })).id
  for (let n=0;n<30 && counts[1]===0;n++) await wait(100)
  assert.ok(counts[0]>=1); assert.ok(counts[1]>=1); assert.equal(counts[2],0)
  await hubs[1].stop()
  await api(`/api/revenues/${id}`,tokens[0],'PUT',{walletId:contexts[0].wallets[0].id,amount:123456,occurredOn:date,note:'Update while client disconnected',rowVersion:1})
  await hubs[1].start(); await hubs[1].invoke('JoinGroup',group)
  const snapshot = await api(`/api/revenues/${id}`,tokens[1])
  assert.equal(snapshot.amount,123456)
  const result = { group, twoClientsReceivedUpdate: true, outsiderJoinDenied:true, otherGroupEvents:counts[2], reconnectRejoinAndSnapshot:true, receivedEvents:counts, checkedAt:new Date().toISOString() }
  await mkdir(root,{recursive:true}); await writeFile(new URL('realtime-test.json',root),JSON.stringify(result,null,2))
  console.log(JSON.stringify(result))
} finally {
  if (id) await api(`/api/revenues/${id}`,tokens[0],'DELETE')
  await Promise.all(hubs.map(h => h.stop()))
}

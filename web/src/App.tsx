import { useCallback, useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { http, setTokens, errorMessage } from './api/httpClient'
import type { Context, Report, Revenue, Rule, Run, Tokens } from './api/contracts'
import { useDashboardHub } from './hooks/useDashboardHub'
import './styles.css'

const today = () => new Date().toLocaleDateString('sv-SE')
const money = (n: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(n)
export default function App() {
  const [logged, setLogged] = useState(false)
  const [email, setEmail] = useState('demo1@revenueflow.local')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [context, setContext] = useState<Context>({ groups: [], wallets: [], categories: [] })
  const [group, setGroup] = useState(0)
  const [tab, setTab] = useState('dashboard')
  const [from, setFrom] = useState(today().slice(0, 8) + '01')
  const [to, setTo] = useState(today())
  const [by, setBy] = useState('Date')
  const [report, setReport] = useState<Report | null>(null)
  const [cache, setCache] = useState('')
  const [revenues, setRevenues] = useState<Revenue[]>([])
  const [rules, setRules] = useState<Rule[]>([])
  const [runs, setRuns] = useState<Run[]>([])
  const [runRule, setRunRule] = useState<number | null>(null)
  const [editing, setEditing] = useState<Revenue | null>(null)
  const [editingRule, setEditingRule] = useState<Rule | null>(null)
  const [walletId, setWallet] = useState(0)
  const [categoryId, setCategory] = useState(0)
  const [amount, setAmount] = useState('')
  const [note, setNote] = useState('')
  const [date, setDate] = useState(today())
  const [frequency, setFrequency] = useState('Daily')
  const [interval, setIntervalValue] = useState(1)
  const [endDate, setEndDate] = useState('')
  const refresh = useCallback(async () => {
    if (!group || !logged) return
    try {
      const [r, items, rs] = await Promise.all([
        http.get<Report>('/reports/revenue', { params: { groupId: group, from, to, groupBy: by } }),
        http.get<Revenue[]>('/revenues', { params: { groupId: group, from, to, pageSize: 100 } }),
        http.get<Rule[]>('/recurring-rules')])
      setReport(r.data); setCache(r.headers['x-report-cache']); setRevenues(items.data)
      setRules(rs.data.filter(x => context.wallets.some(w => w.id === x.walletId && w.groupId === group)))
      setError('')
    } catch (e) { setError(errorMessage(e)) }
  }, [group, logged, from, to, by, context.wallets])
  const realtime = useDashboardHub(logged ? group : 0, refresh)
  useEffect(() => { void refresh() }, [refresh])
  async function action(fn: () => Promise<unknown>) {
    setBusy(true); setError('')
    try { await fn(); await refresh() } catch (e) { setError(errorMessage(e)) } finally { setBusy(false) }
  }
  async function login(e: FormEvent) {
    e.preventDefault(); setBusy(true)
    try {
      setTokens((await http.post<Tokens>('/auth/login', { email, password })).data)
      const c = (await http.get<Context>('/context')).data
      setContext(c); setGroup(c.groups[0]?.groupId ?? 0); setWallet(c.wallets[0]?.id ?? 0); setLogged(true); setError(''); setPassword('')
    } catch (e) { setTokens(null); setError(errorMessage(e)) } finally { setBusy(false) }
  }
  function reset() { setEditing(null); setEditingRule(null); setAmount(''); setNote(''); setDate(today()); setCategory(0); setFrequency('Daily'); setIntervalValue(1); setEndDate('') }
  async function save(e: FormEvent) {
    e.preventDefault()
    await action(async () => {
      const v = { walletId, categoryId: categoryId || null, amount: Number(amount), occurredOn: date, note: note || null }
      if (tab === 'recurring') {
        const rule = { ...v, frequency, intervalValue: interval, startDate: date, endDate: endDate || null,
          timeZoneId: 'Asia/Ho_Chi_Minh', dayOfWeek: frequency === 'Weekly' ? (new Date(date).getDay() || 7) : null, dayOfMonth: frequency === 'Monthly' ? Number(date.slice(-2)) : null }
        if (editingRule) await http.put(`/recurring-rules/${editingRule.id}`, { ...rule, rowVersion: editingRule.rowVersion })
        else await http.post('/recurring-rules', rule)
      }
      else if (editing) await http.put(`/revenues/${editing.id}`, { ...v, rowVersion: editing.rowVersion })
      else await http.post('/revenues', v)
      reset()
    })
  }
  const wallets = context.wallets.filter(w => w.groupId === group)
  const categories = context.categories.filter(c => c.walletId === walletId)
  if (!logged) return <main className="login"><div className="brand">RF <span>RevenueFlow</span></div><h1>Quản lý doanh thu<br />cùng nhóm của bạn</h1><p>Giao dịch, báo cáo và doanh thu định kỳ trong một không gian.</p><form onSubmit={login}><h2>Đăng nhập</h2><label>Email<input type="email" value={email} onChange={e => setEmail(e.target.value)} required /></label><label>Mật khẩu<input type="password" value={password} onChange={e => setPassword(e.target.value)} required /></label>{error && <p role="alert" className="error">{error}</p>}<button disabled={busy}>{busy ? 'Đang đăng nhập…' : 'Đăng nhập'}</button></form></main>
  return <div className="shell"><aside><div className="brand">RF <span>RevenueFlow</span></div><p className="muted">KHÔNG GIAN LÀM VIỆC</p>{[['dashboard','Tổng quan'],['revenues','Doanh thu'],['recurring','Định kỳ']].map(([key,name]) => <button key={key} className={tab === key ? 'nav active' : 'nav'} onClick={() => { setTab(key); reset(); setRuns([]); setRunRule(null) }}>{name}</button>)}<div className="account"><p>{email}</p><button className="secondary" onClick={() => { setLogged(false); setTokens(null); setGroup(0) }}>Đăng xuất</button></div></aside>
    <main><header><div><p className="eyebrow">DOANH THU THEO NHÓM</p><h1>{tab === 'dashboard' ? 'Tổng quan doanh thu' : tab === 'revenues' ? 'Giao dịch doanh thu' : 'Doanh thu định kỳ'}</h1></div><span className="badge">{realtime}</span></header>
      <section className="filters"><label>Nhóm<select value={group} onChange={e => { const id = Number(e.target.value); setGroup(id); setWallet(context.wallets.find(w => w.groupId === id)?.id ?? 0); reset() }}>{context.groups.map(g => <option key={g.groupId} value={g.groupId}>{g.name}</option>)}</select></label><label>Từ ngày<input type="date" value={from} onChange={e => setFrom(e.target.value)} /></label><label>Đến ngày<input type="date" value={to} onChange={e => setTo(e.target.value)} /></label><button className="secondary" disabled={busy} onClick={() => { void refresh() }}>Làm mới</button></section>
      {error && <p className="error" role="alert">{error}</p>}
      {tab === 'dashboard' && <><section className="metrics"><article><p>Tổng doanh thu trong kỳ</p><strong>{money(report?.totalRevenue ?? 0)}</strong><small>{from} — {to}</small></article><article><p>Giao dịch đang hiển thị</p><strong>{revenues.length}</strong><small>Tối đa 100 giao dịch mới nhất</small></article><article><p>Quy tắc đang hoạt động</p><strong>{rules.filter(r => r.isActive && r.nextRunOn).length}</strong><small>Tự động ghi nhận thu nhập</small></article></section><section className="card"><div className="section-title"><h2>Phân bổ doanh thu</h2><select aria-label="Nhóm báo cáo theo" value={by} onChange={e => setBy(e.target.value)}>{[['Date','Ngày'],['Wallet','Ví'],['Category','Danh mục'],['Member','Thành viên']].map(([v,n]) => <option key={v} value={v}>{n}</option>)}</select></div>{report?.breakdown.length ? report.breakdown.map(b => <div className="bar-row" key={b.key}><span>{b.label}</span><div className="bar"><div style={{ width: `${Math.max(1,b.revenue / report.totalRevenue * 100)}%` }} /></div><b>{money(b.revenue)}</b></div>) : <p className="muted">Chưa có doanh thu trong kỳ.</p>}<p className="muted">Cập nhật {report ? new Date(report.generatedAtUtc).toLocaleString('vi-VN') : '…'} · Cache {cache}</p></section></>}
      {tab !== 'dashboard' && <section className="card"><h2>{tab === 'recurring' ? (editingRule ? 'Cập nhật quy tắc định kỳ' : 'Tạo quy tắc định kỳ') : editing ? 'Cập nhật giao dịch' : 'Thêm doanh thu'}</h2><form className="entry" onSubmit={save}><label>Ví<select disabled={!!editingRule} value={walletId} onChange={e => { setWallet(Number(e.target.value)); setCategory(0) }} required>{wallets.map(w => <option key={w.id} value={w.id}>{w.name}</option>)}</select></label><label>Danh mục<select value={categoryId} onChange={e => setCategory(Number(e.target.value))}><option value={0}>Không phân loại</option>{categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></label><label>Số tiền<input type="number" min="0.0001" step="0.0001" value={amount} onChange={e => setAmount(e.target.value)} required /></label><label>{tab === 'recurring' ? 'Ngày bắt đầu' : 'Ngày phát sinh'}<input type="date" value={date} onChange={e => setDate(e.target.value)} required /></label><label className="wide">Ghi chú<input maxLength={500} value={note} onChange={e => setNote(e.target.value)} /></label>{tab === 'recurring' && <><label>Tần suất<select value={frequency} onChange={e => setFrequency(e.target.value)}><option value="Daily">Hàng ngày</option><option value="Weekly">Hàng tuần</option><option value="Monthly">Hàng tháng</option></select></label><label>Chu kỳ<input type="number" min={1} max={365} value={interval} onChange={e => setIntervalValue(Number(e.target.value))} /></label><label>Ngày kết thúc<input type="date" value={endDate} onChange={e => setEndDate(e.target.value)} /></label></>}<div className="actions"><button disabled={busy || !walletId}>Lưu</button>{(editing || editingRule) && <button type="button" className="secondary" onClick={reset}>Hủy sửa</button>}</div></form></section>}
      {tab === 'revenues' && <section className="card"><h2>Danh sách giao dịch</h2><div className="table-wrap"><table><thead><tr><th>Ngày</th><th>Ghi chú</th><th>Số tiền</th><th>Thao tác</th></tr></thead><tbody>{revenues.map(r => <tr key={r.id}><td>{r.occurredOn}</td><td>{r.note || '—'}</td><td>{money(r.amount)}</td><td><button className="text" disabled={busy} onClick={() => { setEditing(r); setWallet(r.walletId); setCategory(r.categoryId ?? 0); setAmount(String(r.amount)); setDate(r.occurredOn); setNote(r.note ?? '') }}>Sửa</button><button className="text danger" disabled={busy} onClick={() => { if (confirm('Xóa giao dịch này?')) void action(() => http.delete(`/revenues/${r.id}`)) }}>Xóa</button></td></tr>)}</tbody></table>{!revenues.length && <p className="muted">Chưa có giao dịch.</p>}</div></section>}
      {tab === 'recurring' && <section className="card"><h2>Quy tắc định kỳ</h2>{rules.map(r => <div className="rule" key={r.id}><div><b>{r.note || `Quy tắc #${r.id}`}</b><p>{money(r.amount)} · {r.frequency} · Kỳ tiếp theo {r.nextRunOn ?? 'Đã kết thúc'}</p><span className="badge">{!r.nextRunOn ? 'Đã kết thúc' : r.isActive ? 'Đang hoạt động' : 'Đã tắt'}</span></div><div className="actions"><button className="secondary" disabled={busy} onClick={() => { setEditingRule(r); setWallet(r.walletId); setCategory(r.categoryId ?? 0); setAmount(String(r.amount)); setDate(r.startDate); setNote(r.note ?? ""); setFrequency(r.frequency); setIntervalValue(r.intervalValue); setEndDate(r.endDate ?? "") }}>Sửa</button><button className="secondary" disabled={busy} onClick={() => { void action(() => http.patch(`/recurring-rules/${r.id}/active`, { isActive: !r.isActive, rowVersion: r.rowVersion })) }}>{r.isActive ? 'Tắt' : 'Bật'}</button><button disabled={busy || !r.isActive || !r.nextRunOn} onClick={() => { void action(() => http.post(`/recurring-rules/${r.id}/trigger`)) }}>Chạy ngay</button><button className="secondary" disabled={busy} onClick={() => { void action(async () => { setRunRule(r.id); setRuns((await http.get<Run[]>(`/recurring-rules/${r.id}/runs`)).data) }) }}>Nhật ký</button><button className="text danger" disabled={busy} onClick={() => { if (confirm('Xóa quy tắc chưa chạy này?')) void action(() => http.delete(`/recurring-rules/${r.id}`)) }}>Xóa</button></div></div>)}{!rules.length && <p className="muted">Chưa có quy tắc định kỳ.</p>}{runRule !== null && <><h3>Nhật ký quy tắc #{runRule}</h3>{runs.length ? runs.map(r => <p key={r.id}>{r.occurrenceDate} · <b>{r.status}</b> {r.error}</p>) : <p>Chưa có lần chạy.</p>}</>}</section>}
    </main></div>
}

import { useEffect, useRef, useState } from 'react';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Link } from 'react-router-dom';
import { Users, Plus, ArrowLeft, Pencil, Trash2, Wallet, TrendingDown, TrendingUp, AlertTriangle } from 'lucide-react';
import BottomNavigation from '../components/BottomNavigation';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';

type Group = { id: number; name: string; budget: number; archived: boolean; ownerId: number; version: number };
type Entry = { entry_id: number; title: string; category: string; kind: string; amount: number; date: string; payer_id: number; created_by: number; version: number };
type Detail = { group: { name: string; budget: number; archived: boolean; version: number }; canManage: boolean; members: { userId: number; name: string; isOwner: boolean }[]; entries: Entry[]; summary: { income: number; expense: number; balance: number; budget: number; overBudget: boolean; byCategory: { category: string; amount: number }[] } };
const money = (n: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(n);
const today = () => new Date().toLocaleDateString('en-CA');
const emptyEntry = () => ({ title: '', amount: '', kind: 'expense', category: 'Ăn uống', date: today(), payer_id: '' });

export default function GroupExpensesPage({ admin = false }: { admin?: boolean }) {
  const base = admin ? '/api/admin/groups' : '/api/groups';
  const [groups, setGroups] = useState<Group[]>([]);
  const [selected, setSelected] = useState<number | null>(null);
  const [detail, setDetail] = useState<Detail | null>(null);
  const [month, setMonth] = useState(today().slice(0, 7));
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [groupForm, setGroupForm] = useState({ name: '', budget: '0' });
  const [email, setEmail] = useState('');
  const [form, setForm] = useState(emptyEntry);
  const [editing, setEditing] = useState<Entry | null>(null);
  const [liveStatus, setLiveStatus] = useState('Đang kết nối cập nhật trực tiếp…');
  const refreshRef = useRef<() => Promise<void>>(async () => {});
  const selectedRef = useRef(selected);
  const monthRef = useRef(month);
  const refreshSequence = useRef(0);
  selectedRef.current = selected; monthRef.current = month;
  const userId = Number(JSON.parse(localStorage.getItem('userInfo') || '{}').user_id || 0);

  async function request(path: string, method = 'GET', body?: unknown) {
    const response = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json' }, ...(body === undefined ? {} : { body: JSON.stringify(body) }) });
    const data = await response.json();
    if (!response.ok) throw new Error(data.message || 'Không thể xử lý yêu cầu');
    return data;
  }
  async function refresh(id = selected) {
    const sequence = ++refreshSequence.current;
    const requestedMonth = month;
    const list = await request('');
    if (sequence === refreshSequence.current) setGroups(list.groups);
    if (id !== null) {
      const d = await request(`/${id}?month=${requestedMonth}`);
      if (sequence === refreshSequence.current && selectedRef.current === id && monthRef.current === requestedMonth) setDetail(d);
    }
  }
  refreshRef.current = () => refresh();
  useEffect(() => {
    let active = true;
    let retry: ReturnType<typeof setTimeout> | undefined;
    let changed: ReturnType<typeof setTimeout> | undefined;
    const connection = new HubConnectionBuilder().withUrl('/hubs/groups', {
      accessTokenFactory: () => localStorage.getItem(admin ? 'adminToken' : 'token') || '',
    }).withAutomaticReconnect().configureLogging(LogLevel.Error).build();
    const reload = () => {
      if (active) refreshRef.current().catch(e => { if (active) setError(e.message); });
    };
    const scheduleReload = () => { clearTimeout(changed); changed = setTimeout(reload, 200); };
    connection.on('GroupChanged', scheduleReload);
    connection.onreconnecting(() => { if (active) setLiveStatus('Mất kết nối — đang kết nối lại…'); });
    connection.onreconnected(() => { if (active) { setLiveStatus('Đang cập nhật trực tiếp'); reload(); } });
    async function start() {
      try {
        await connection.start();
        if (active) { setLiveStatus('Đang cập nhật trực tiếp'); reload(); }
        else await connection.stop();
      } catch {
        if (active) { setLiveStatus('Chưa có cập nhật trực tiếp — vẫn có thể tải lại dữ liệu'); retry = setTimeout(start, 5000); }
      }
    }
    connection.onclose(() => { if (active) { setLiveStatus('Đang kết nối lại…'); retry = setTimeout(start, 5000); } });
    void start();
    return () => { active = false; clearTimeout(retry); clearTimeout(changed); connection.off('GroupChanged', scheduleReload); void connection.stop(); };
  }, [admin]);
  useEffect(() => {
    let active = true; setLoading(true); setError('');
    request('').then(d => { if (active) setGroups(d.groups); }).catch(e => { if (active) setError(e.message); }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [base]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => {
    let active = true; setDetail(null); setEditing(null); setForm(emptyEntry());
    if (selected === null) return;
    setLoading(true); setError('');
    request(`/${selected}?month=${month}`).then(d => { if (active) setDetail(d); }).catch(e => { if (active) setError(e.message); }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [selected, month, base]); // eslint-disable-line react-hooks/exhaustive-deps
  async function mutate(action: () => Promise<void>) {
    if (busy) return; setBusy(true); setError(''); setNotice('');
    try { await action(); setNotice('Đã lưu thay đổi.'); } catch (e) { setError(e instanceof Error ? e.message : 'Không thể lưu'); }
    finally { setBusy(false); }
  }
  async function saveEntry(e: React.FormEvent) {
    e.preventDefault();
    await mutate(async () => {
      await request(`/${selected}/entries${editing ? '/' + editing.entry_id : ''}`, editing ? 'PUT' : 'POST', { ...form, amount: Number(form.amount), ...(form.payer_id ? { payer_id: Number(form.payer_id) } : {}), ...(editing ? { version: editing.version } : {}) });
      setForm(emptyEntry()); setEditing(null); await refresh();
    });
  }
  const writable = detail && !detail.group.archived && !admin;
  return <div className="min-h-screen bg-slate-50 pb-28">
    <header className="bg-white border-b"><div className="max-w-7xl mx-auto p-5 flex items-center gap-4"><Link to={admin ? '/admin/dashboard' : '/wallet'} aria-label="Quay lại"><ArrowLeft /></Link><div><h1 className="text-2xl font-bold flex gap-2 items-center"><Users />{admin ? 'Quản trị chi tiêu nhóm' : 'Chi tiêu nhóm'}</h1><p className="text-slate-500 text-sm">Sổ thu chi chung · ngân sách theo tháng</p></div></div></header>
    <main className="max-w-7xl mx-auto p-4 md:p-6 space-y-4">
      <div className="flex justify-between items-center gap-3 text-sm text-slate-500"><span role="status">{liveStatus}</span><Button variant="outline" size="sm" disabled={busy || loading} onClick={() => mutate(async () => { await refresh(); })}>Tải lại</Button></div>
      <p className="rounded-xl bg-blue-50 text-blue-900 p-4 text-sm">Các khoản ghi sổ giúp theo dõi thu chi của nhóm và không tự trừ tiền trong ví cá nhân.{admin && ' Quản trị viên xem dữ liệu và lưu trữ hoặc mở lại nhóm; không sửa các khoản thu chi của thành viên.'}</p>
      {error && <div role="alert" className="p-4 bg-red-50 text-red-800 rounded-xl">{error} <button className="underline ml-2" onClick={() => mutate(async () => { await refresh(); })}>Tải lại</button></div>}
      {notice && <div role="status" className="text-emerald-700">{notice}</div>}
      <div className="grid lg:grid-cols-[280px_1fr] gap-5">
        <aside className="space-y-4">
          {!admin && <form className="bg-white p-4 rounded-xl border space-y-3" onSubmit={e => { e.preventDefault(); mutate(async () => { const d = await request('', 'POST', { name: groupForm.name, budget: Number(groupForm.budget) }); await refresh(null); setSelected(d.id); setGroupForm({ name: '', budget: '0' }); }); }}>
            <h2 className="font-semibold">Tạo nhóm mới</h2><label className="block text-sm">Tên nhóm<Input required maxLength={100} value={groupForm.name} onChange={e => setGroupForm({ ...groupForm, name: e.target.value })} /></label><label className="block text-sm">Ngân sách mỗi tháng (VND)<Input type="number" required min="0" max="1000000000000" step="1" value={groupForm.budget} onChange={e => setGroupForm({ ...groupForm, budget: e.target.value })} /></label><Button disabled={busy}><Plus className="w-4 h-4 mr-1" />Tạo nhóm</Button>
          </form>}
          <section className="bg-white rounded-xl border p-3"><h2 className="font-semibold p-2">{admin ? 'Tất cả nhóm' : 'Nhóm của tôi'} ({groups.length})</h2>{groups.length === 0 && !loading && <p className="p-2 text-sm text-slate-500">Chưa có nhóm nào.</p>}{groups.map(g => <button key={g.id} disabled={busy} onClick={() => { setSelected(g.id); setError(''); setNotice(''); }} className={`w-full text-left p-3 rounded-lg mb-1 ${selected === g.id ? 'bg-slate-900 text-white' : 'hover:bg-slate-100'}`}><strong className="block break-words">{g.name}</strong><span className="text-xs">{g.archived ? 'Đã lưu trữ' : 'Đang hoạt động'} · {money(g.budget)}/tháng</span></button>)}</section>
        </aside>
        <div className="space-y-4">
          {loading && <p role="status">Đang tải dữ liệu…</p>}
          {!detail && !loading && <div className="bg-white rounded-xl border p-12 text-center text-slate-500"><Users className="mx-auto mb-3" />Chọn một nhóm để xem thu chi và thành viên.</div>}
          {detail && <>
            <section className="bg-white p-5 border rounded-xl space-y-4"><div className="flex flex-wrap justify-between gap-3"><div><h2 className="text-xl font-bold">{detail.group.name}</h2>{detail.group.archived && <span className="text-amber-700">Nhóm đã lưu trữ — chỉ xem</span>}</div><label className="text-sm">Tháng thống kê<Input aria-label="Tháng thống kê" type="month" required value={month} onChange={e => { if (e.target.value) setMonth(e.target.value); }} /></label></div>
              <div className="grid sm:grid-cols-3 gap-3">{[[TrendingUp, 'Tổng thu', detail.summary.income], [TrendingDown, 'Tổng chi', detail.summary.expense], [Wallet, 'Thu trừ chi', detail.summary.balance]].map(([Icon, label, amount]) => { const Metric = Icon as typeof Wallet; return <div key={String(label)} className="bg-slate-50 p-4 rounded-lg"><p className="flex items-center gap-2 text-sm text-slate-500"><Metric className="w-4 h-4" />{String(label)}</p><p className="text-lg font-bold">{money(Number(amount))}</p></div>; })}</div>
              <p>Ngân sách tháng: <strong>{money(detail.summary.budget)}</strong> {detail.summary.budget === 0 && '(chưa đặt giới hạn)'}</p>{detail.summary.overBudget && <p role="status" className="flex gap-2 text-red-700 bg-red-50 p-3 rounded-lg"><AlertTriangle />Vượt ngân sách {money(detail.summary.expense - detail.summary.budget)}</p>}
              {detail.summary.byCategory.length > 0 && <div className="flex flex-wrap gap-2">{detail.summary.byCategory.map(c => <span key={c.category} className="rounded-full border px-3 py-1 text-sm">{c.category}: {money(c.amount)}</span>)}</div>}
              {admin && <Button disabled={busy} variant="outline" onClick={() => { if (window.confirm(detail.group.archived ? 'Mở lại nhóm này?' : 'Lưu trữ nhóm? Thành viên vẫn xem được nhưng không thể ghi thêm.')) mutate(async () => { await request(`/${selected}/status`, 'PATCH', { archived: !detail.group.archived, version: detail.group.version }); await refresh(); }); }}>{detail.group.archived ? 'Mở lại nhóm' : 'Lưu trữ nhóm'}</Button>}
              {writable && detail.canManage && <form className="flex flex-wrap gap-2" onSubmit={e => { e.preventDefault(); const data = new FormData(e.currentTarget); mutate(async () => { await request(`/${selected}`, 'PATCH', { name: data.get('name'), budget: Number(data.get('budget')), version: detail.group.version }); await refresh(); }); }}><Input aria-label="Đổi tên nhóm" name="name" required maxLength={100} defaultValue={detail.group.name} key={'name' + detail.group.version} className="sm:w-52" /><Input aria-label="Đổi ngân sách" name="budget" type="number" required min="0" step="1" max="1000000000000" defaultValue={detail.group.budget} key={'budget' + detail.group.version} className="sm:w-48" /><Button disabled={busy} variant="outline">Cập nhật nhóm</Button></form>}
            </section>
            <section className="bg-white rounded-xl border p-5 space-y-3"><h2 className="font-semibold">Thành viên ({detail.members.length})</h2><div className="flex flex-wrap gap-2">{detail.members.map(m => <span key={m.userId} className="border rounded-lg p-2 text-sm">{m.name}{m.isOwner && ' · Trưởng nhóm'}{writable && detail.canManage && !m.isOwner && <button aria-label={'Xóa ' + m.name} disabled={busy} className="text-red-600 ml-3" onClick={() => { if (window.confirm('Xóa thành viên khỏi nhóm? Lịch sử thu chi vẫn được giữ.')) mutate(async () => { await request(`/${selected}/members/${m.userId}`, 'DELETE'); await refresh(); }); }}>×</button>}</span>)}</div>{writable && detail.canManage && <form className="flex gap-2" onSubmit={e => { e.preventDefault(); mutate(async () => { await request(`/${selected}/members`, 'POST', { email }); setEmail(''); await refresh(); }); }}><Input aria-label="Email thành viên" type="email" required placeholder="Email tài khoản HTD PAY" value={email} onChange={e => setEmail(e.target.value)} /><Button disabled={busy}>Thêm thành viên</Button></form>}</section>
            {writable && <form onSubmit={saveEntry} className="bg-white p-5 rounded-xl border space-y-3"><h2 className="font-semibold">{editing ? 'Sửa khoản thu chi' : 'Ghi khoản thu chi'}</h2><div className="grid sm:grid-cols-2 gap-3"><label className="text-sm">Nội dung<Input required maxLength={150} value={form.title} onChange={e => setForm({ ...form, title: e.target.value })} /></label><label className="text-sm">Số tiền (VND)<Input type="number" required min="1" step="1" max="1000000000000" value={form.amount} onChange={e => setForm({ ...form, amount: e.target.value })} /></label><label className="text-sm">Loại<select className="block w-full border rounded-md h-10 px-3" value={form.kind} onChange={e => setForm({ ...form, kind: e.target.value })}><option value="expense">Chi</option><option value="income">Thu</option></select></label><label className="text-sm">Danh mục<Input required maxLength={60} value={form.category} onChange={e => setForm({ ...form, category: e.target.value })} /></label><label className="text-sm">Ngày phát sinh<Input type="date" required value={form.date} onChange={e => setForm({ ...form, date: e.target.value })} /></label><label className="text-sm">Người thanh toán / nhận tiền<select className="block w-full border rounded-md h-10 px-3" value={form.payer_id} onChange={e => setForm({ ...form, payer_id: e.target.value })}><option value="">Tôi</option>{detail.members.map(m => <option key={m.userId} value={m.userId}>{m.name}</option>)}</select></label></div><Button disabled={busy}>{busy ? 'Đang lưu…' : editing ? 'Lưu chỉnh sửa' : 'Thêm khoản thu chi'}</Button>{editing && <Button type="button" variant="ghost" onClick={() => { setEditing(null); setForm(emptyEntry()); }}>Hủy sửa</Button>}</form>}
            <section className="bg-white border rounded-xl p-5"><h2 className="font-semibold mb-3">Lịch sử thu chi · {month}</h2>{detail.entries.length === 0 ? <p className="text-slate-500">Tháng này chưa có khoản thu chi.</p> : <div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="text-left border-b"><th className="p-2">Ngày</th><th className="p-2">Nội dung</th><th className="p-2">Người thanh toán</th><th className="p-2 text-right">Số tiền</th><th className="p-2">Thao tác</th></tr></thead><tbody>{detail.entries.map(t => <tr key={t.entry_id} className="border-b"><td className="p-2 whitespace-nowrap">{t.date}</td><td className="p-2"><strong>{t.title}</strong><p className="text-slate-500">{t.category}</p></td><td className="p-2">{detail.members.find(m => m.userId === t.payer_id)?.name || 'Thành viên cũ'}</td><td className={`p-2 text-right whitespace-nowrap ${t.kind === 'expense' ? 'text-red-700' : 'text-emerald-700'}`}>{t.kind === 'expense' ? '−' : '+'}{money(t.amount)}</td><td className="p-2">{writable && (detail.canManage || t.created_by === userId) && <div className="flex gap-1"><Button size="icon" variant="ghost" disabled={busy} aria-label={'Sửa ' + t.title} onClick={() => { setEditing(t); setForm({ title: t.title, amount: String(t.amount), category: t.category, kind: t.kind, date: t.date, payer_id: String(t.payer_id) }); }}><Pencil className="w-4 h-4" /></Button><Button size="icon" variant="ghost" disabled={busy} aria-label={'Xóa ' + t.title} onClick={() => { if (window.confirm('Xóa khoản thu chi này khỏi sổ nhóm?')) mutate(async () => { await request(`/${selected}/entries/${t.entry_id}`, 'DELETE', { version: t.version }); await refresh(); }); }}><Trash2 className="w-4 h-4 text-red-600" /></Button></div>}</td></tr>)}</tbody></table></div>}</section>
          </>}
        </div>
      </div>
    </main>{!admin && <BottomNavigation />}
  </div>;
}

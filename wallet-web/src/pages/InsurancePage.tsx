import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../components/ui/button';
import ServiceModal from '../components/ServiceModals';
import BottomNavigation from '../components/BottomNavigation';

interface Policy { id: string; name: string; premium: number; coverage: number; endDate: string; status: string }
export default function InsurancePage() {
  const navigate = useNavigate();
  const [policies, setPolicies] = useState<Policy[]>([]);
  const [error, setError] = useState('');
  const [creating, setCreating] = useState(false);
  const [busy, setBusy] = useState('');
  const user = JSON.parse(localStorage.getItem('userInfo') || '{}');
  const money = (value: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
  async function load() {
    const response = await fetch(`/api/insurance/${user.user_id}`); const data = await response.json();
    if (!response.ok) throw new Error(data.message); setPolicies(data.policies || []);
  }
  useEffect(() => { load().catch(e => setError(e.message)); }, []);
  async function update(policy: Policy, action: 'renew' | 'cancel') {
    setBusy(policy.id); setError('');
    try {
      const response = await fetch(`/api/insurance/${action}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ user_id: user.user_id, id: policy.id, term: 1 }) });
      const data = await response.json(); if (!response.ok) throw new Error(data.message); await load();
    } catch (e) { setError(e instanceof Error ? e.message : 'Không thể cập nhật'); } finally { setBusy(''); }
  }
  return <main className="max-w-2xl mx-auto p-6 pb-24 space-y-5">
    <Button variant="outline" onClick={() => navigate('/profile')}>Quay lại</Button><h1 className="text-2xl font-bold">Hợp đồng bảo hiểm</h1>
    <Button onClick={() => setCreating(true)}>Mua bảo hiểm</Button><p className="text-sm text-gray-600">Hợp đồng mô phỏng phục vụ đồ án. Phí được thanh toán từ số dư ví.</p>
    {error && <p role="alert" className="text-red-600">{error}</p>}
    {!policies.length && <p>Chưa có hợp đồng.</p>}
    {policies.map(policy => <article key={policy.id} className="border rounded-xl p-4 space-y-2">
      <h2 className="font-semibold">{policy.name}</h2><p>Phí: {money(policy.premium)} · Quyền lợi: {money(policy.coverage)}</p><p>Hết hạn: {policy.endDate} · {policy.status === 'active' ? 'Đang hoạt động' : 'Đã hủy'}</p>
      <div className="flex gap-3"><Button disabled={busy === policy.id} onClick={() => update(policy, 'renew')}>Gia hạn 1 năm · {money(policy.premium)}</Button>{policy.status === 'active' && <Button disabled={busy === policy.id} variant="outline" onClick={() => update(policy, 'cancel')}>Hủy hợp đồng</Button>}</div>
    </article>)}<ServiceModal isOpen={creating} onClose={() => { setCreating(false); load().catch(e => setError(e.message)); }} type="insurance" /><BottomNavigation />
  </main>;
}

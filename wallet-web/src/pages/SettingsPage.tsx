import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import BottomNavigation from '../components/BottomNavigation';

export default function SettingsPage() {
  const navigate = useNavigate();
  const [form, setForm] = useState({ name: '', phone: '', notificationsEnabled: true, hideBalance: false });
  const [message, setMessage] = useState('');
  const [saving, setSaving] = useState(false);
  useEffect(() => {
    fetch('/api/auth/me').then(r => r.json()).then(data => {
      if (!data.success) throw new Error(data.message);
      setForm({ name: data.user.name || '', phone: data.user.phone || '', notificationsEnabled: data.user.settings?.notificationsEnabled !== false, hideBalance: !!data.user.settings?.hideBalance });
    }).catch(e => setMessage(e.message));
  }, []);
  async function save(e: React.FormEvent) {
    e.preventDefault(); setSaving(true); setMessage('');
    try {
      const response = await fetch('/api/auth/profile', { method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name: form.name, phone: form.phone, settings: { notificationsEnabled: form.notificationsEnabled, hideBalance: form.hideBalance } }) });
      const data = await response.json();
      if (!response.ok) throw new Error(data.message);
      localStorage.setItem('userInfo', JSON.stringify(data.user)); setMessage('Đã lưu cài đặt.');
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Không thể lưu'); }
    finally { setSaving(false); }
  }
  return <main className="max-w-xl mx-auto p-6 pb-24 space-y-5">
    <Button variant="outline" onClick={() => navigate('/profile')}>Quay lại</Button>
    <h1 className="text-2xl font-bold">Hồ sơ và cài đặt</h1>
    <form onSubmit={save} className="space-y-5">
      <label className="block">Họ tên<Input required maxLength={100} value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} /></label>
      <label className="block">Số điện thoại<Input required inputMode="tel" value={form.phone} onChange={e => setForm({ ...form, phone: e.target.value })} /></label>
      <p className="text-sm text-gray-600">Tài khoản Google cần bổ sung số điện thoại để nhận chuyển tiền qua ví.</p>
      <label className="flex gap-3"><input type="checkbox" checked={form.notificationsEnabled} onChange={e => setForm({ ...form, notificationsEnabled: e.target.checked })} />Nhận thông báo giao dịch trong ứng dụng</label>
      <label className="flex gap-3"><input type="checkbox" checked={form.hideBalance} onChange={e => setForm({ ...form, hideBalance: e.target.checked })} />Ẩn số dư khi mở ví</label>
      <p role="status">{message}</p><Button disabled={saving}>{saving ? 'Đang lưu...' : 'Lưu cài đặt'}</Button>
    </form><BottomNavigation />
  </main>;
}

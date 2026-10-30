import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import BottomNavigation from '../components/BottomNavigation';
import { useAuthConfiguration } from '../services/auth-config';

interface Order { order_id: string; amount: number; type: string; status: string; created_at: string }
export default function PaymentsPage() {
  const navigate = useNavigate();
  const config = useAuthConfiguration();
  const [type, setType] = useState('wallet_topup');
  const [amount, setAmount] = useState('100000');
  const [billType, setBillType] = useState('electricity');
  const [billCode, setBillCode] = useState('');
  const [pin, setPin] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [orders, setOrders] = useState<Order[]>([]);
  useEffect(() => { fetch('/api/payments/orders').then(r => r.json()).then(data => setOrders(data.orders || [])).catch(e => setError(e.message)); }, []);
  async function checkout(e: React.FormEvent) {
    e.preventDefault(); if (busy) return; setBusy(true); setError('');
    try {
      const response = await fetch('/api/payments/vnpay/create', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ type, amount: Number(amount), pin, ...(type === 'bill_payment' && { bill_type: billType, bill_code: billCode }) }) });
      const data = await response.json(); if (!response.ok) throw new Error(data.message);
      const url = new URL(data.paymentUrl); if (url.protocol !== 'https:' || url.hostname !== 'sandbox.vnpayment.vn') throw new Error('Địa chỉ VNPay sandbox không hợp lệ');
      window.location.assign(url.href);
    } catch (e) { setError(e instanceof Error ? e.message : 'Không thể tạo thanh toán'); setBusy(false); }
  }
  return <main className="max-w-2xl mx-auto p-4 sm:p-6 pb-28 space-y-5">
    <header className="rounded-3xl bg-gradient-to-br from-pink-700 to-rose-500 p-6 text-white shadow-lg shadow-pink-200"><Button variant="ghost" className="text-white hover:bg-white/15 hover:text-white mb-4" onClick={() => navigate('/wallet')}>← Về ví</Button><p className="text-xs font-semibold tracking-[0.2em] text-pink-100">HTD PAY · THANH TOÁN AN TOÀN</p><h1 className="text-3xl font-bold mt-2">Nạp tiền & thanh toán</h1><p className="text-pink-100 mt-2 text-sm">Một chạm đến cổng VNPay sandbox.</p></header>
    <p className="text-sm text-gray-600">Môi trường kiểm thử. Dùng tài khoản/thẻ test do VNPay cung cấp.</p>
    {!config.vnpayEnabled && <p role="alert">Backend chưa có cấu hình VNPay sandbox.</p>}
    <form onSubmit={checkout} className="space-y-5 border border-pink-100 rounded-3xl p-5 sm:p-7 bg-white shadow-sm">
      <label className="block">Mục đích<select className="block border rounded p-2 w-full" value={type} onChange={e => setType(e.target.value)}><option value="wallet_topup">Nạp tiền vào ví</option><option value="bill_payment">Thanh toán hóa đơn</option></select></label>
      {type === 'bill_payment' && <><label className="block">Loại hóa đơn<select className="block border rounded p-2 w-full" value={billType} onChange={e => setBillType(e.target.value)}><option value="electricity">Điện</option><option value="water">Nước</option><option value="internet">Internet</option><option value="tv">Truyền hình</option></select></label><label className="block">Mã khách hàng<Input required value={billCode} onChange={e => setBillCode(e.target.value)} /></label></>}
      <label className="block">Số tiền (VND)<Input required type="number" min={10000} max={50000000} step={1} value={amount} onChange={e => setAmount(e.target.value)} /></label>
      <div className="grid grid-cols-3 gap-2">{[50000, 100000, 200000].map(value => <Button key={value} type="button" variant="outline" className={Number(amount) === value ? "border-pink-600 bg-pink-50 text-pink-700" : ""} onClick={() => setAmount(String(value))}>{value.toLocaleString('vi-VN')} đ</Button>)}</div><label className="block">PIN giao dịch<Input required type="password" inputMode="numeric" pattern="[0-9]{6}" maxLength={6} value={pin} onChange={e => setPin(e.target.value)} /></label>
      <Button type="button" variant="outline" onClick={() => navigate('/security')}>Thiết lập PIN</Button>
      {error && <p role="alert" className="text-red-600">{error}</p>}
      <Button className="w-full rounded-xl py-6 text-base" disabled={busy || !config.vnpayEnabled}>{busy ? 'Đang chuyển đến VNPay...' : 'Tiếp tục đến VNPay sandbox'}</Button>
    </form>
    <h2 className="text-xl font-semibold">Lịch sử thanh toán qua cổng</h2>
    {!orders.length && <p>Chưa có đơn thanh toán.</p>}
    {orders.map(order => <article className="border border-pink-100 bg-white rounded-2xl p-4 space-y-1" key={order.order_id}><p className="break-all font-medium">{order.order_id}</p><p>{new Intl.NumberFormat('vi-VN').format(order.amount)} VND · {order.status}</p><p>{new Date(order.created_at).toLocaleString('vi-VN')}</p></article>)}
    <BottomNavigation />
  </main>;
}

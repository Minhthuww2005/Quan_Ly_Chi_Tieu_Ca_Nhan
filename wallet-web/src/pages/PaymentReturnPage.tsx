import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../components/ui/button';
import { useAuthConfiguration } from '../services/auth-config';

export default function PaymentReturnPage() {
  const navigate = useNavigate();
  const config = useAuthConfiguration();
  const [message, setMessage] = useState('Đang xác minh kết quả từ VNPay...');
  const [status, setStatus] = useState('');
  useEffect(() => {
    let stopped = false;
    let interval: ReturnType<typeof setInterval> | undefined;
    let polls = 0;
    let complete = false;
    async function verify() {
      try {
        const query = window.location.search;
        const response = await fetch(`/api/payments/vnpay/verify${query}`); const data = await response.json();
        if (stopped) return;
        if (!data.signatureValid) { setMessage(data.Message || data.message || 'Kết quả VNPay không hợp lệ.'); return; }
        if (!localStorage.getItem('token')) { setMessage('Kết quả hợp lệ. Đăng nhập lại để xem trạng thái đơn hàng.'); return; }
        if (config.vnpayLocalSettlement) {
          const confirmation = await fetch(`/api/payments/vnpay/confirm-local${query}`, { method: 'POST', body: '{}' });
          const result = await confirmation.json();
          if (!confirmation.ok || !['00', '02'].includes(result.RspCode)) throw new Error(result.Message || result.message || 'Không xác nhận được giao dịch');
        }
        if (data.responseCode !== '00') { setMessage(`Thanh toán chưa thành công (mã ${data.responseCode}).`); return; }
        setMessage('VNPay trả kết quả hợp lệ. Đang chờ backend nhận IPN để quyết toán.');
        if (!localStorage.getItem('token')) { setMessage('Kết quả hợp lệ. Đăng nhập lại để xem trạng thái đơn hàng.'); return; }
        async function poll() {
          const result = await fetch(`/api/payments/status/${data.orderId}`); const latest = await result.json();
          if (stopped) return; const state = latest.order?.status;
          setStatus(state || '');
          if (state === 'success') { complete = true; setMessage('Thanh toán đã được xác nhận. Dữ liệu ví và hóa đơn đã cập nhật.'); if (interval) clearInterval(interval); }
          else if (state === 'expired') { complete = true; setMessage('Đơn đã hết hạn. Nếu ngân hàng đã ghi nhận thanh toán, giao dịch cần được đối soát.'); if (interval) clearInterval(interval); }
          else if (state === 'failed') { complete = true; setMessage('Thanh toán không thành công. Ví không được cộng tiền.'); if (interval) clearInterval(interval); }
          else if (++polls >= 20) { setMessage('Chưa nhận được IPN. Đơn đang chờ xác nhận; kiểm tra lại lịch sử thanh toán.'); if (interval) clearInterval(interval); }
        }
        await poll(); if (!stopped && !complete) interval = setInterval(() => poll().catch(() => setMessage('Không tải được trạng thái đơn hàng.')), 3000);
      } catch (e) { if (!stopped) setMessage(e instanceof Error ? e.message : 'Không thể xác minh kết quả'); }
    }
    verify(); return () => { stopped = true; if (interval) clearInterval(interval); };
  }, [config.vnpayLocalSettlement]);
  return <main className="max-w-xl mx-auto my-10 p-8 space-y-5 rounded-3xl border border-pink-100 bg-white shadow-xl shadow-pink-100/50"><h1 className="text-2xl font-bold">Kết quả VNPay sandbox</h1><p role="status">{message}</p>{status && <p>Trạng thái đơn: {status}</p>}<div className="flex gap-3"><Button onClick={() => navigate('/wallet')}>Về ví</Button><Button variant="outline" onClick={() => navigate('/payments')}>Xem thanh toán</Button></div></main>;
}

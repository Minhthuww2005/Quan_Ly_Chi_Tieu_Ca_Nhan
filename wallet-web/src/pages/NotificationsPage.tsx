import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { notificationAPI } from '../services/api';
import { Button } from '../components/ui/button';
import BottomNavigation from '../components/BottomNavigation';

interface Notice { notification_id: number; title: string; message?: string; content?: string; createdAt?: string; isSeen: boolean }
export default function NotificationsPage() {
  const navigate = useNavigate();
  const [notices, setNotices] = useState<Notice[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    const user = JSON.parse(localStorage.getItem('userInfo') || '{}');
    notificationAPI.getNotifications(user.user_id).then(data => setNotices(data.notifications || [])).catch(e => setError(e.message)).finally(() => setLoading(false));
  }, []);
  async function read(notice: Notice) {
    try { await notificationAPI.markAsRead(notice.notification_id); setNotices(items => items.map(item => item.notification_id === notice.notification_id ? { ...item, isSeen: true } : item)); }
    catch (e) { setError(e instanceof Error ? e.message : 'Không thể cập nhật'); }
  }
  return <main className="max-w-2xl mx-auto p-6 pb-24 space-y-5">
    <Button variant="outline" onClick={() => navigate('/wallet')}>Về ví</Button><h1 className="text-2xl font-bold">Thông báo</h1>
    {error && <p role="alert" className="text-red-600">{error}</p>}
    {loading ? <p>Đang tải...</p> : !notices.length ? <p>Chưa có thông báo.</p> : notices.map(notice => <article key={notice.notification_id} className={`border rounded-xl p-4 space-y-2 ${notice.isSeen ? 'bg-white' : 'bg-blue-50'}`}>
      <h2 className="font-semibold">{notice.title}</h2><p>{notice.message || notice.content}</p>
      {notice.createdAt && <time>{new Date(notice.createdAt).toLocaleString('vi-VN')}</time>}
      {!notice.isSeen && <Button variant="outline" onClick={() => read(notice)}>Đánh dấu đã đọc</Button>}
    </article>)}<BottomNavigation />
  </main>;
}

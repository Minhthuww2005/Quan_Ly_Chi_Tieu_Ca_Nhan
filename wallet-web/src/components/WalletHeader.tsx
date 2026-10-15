import { Bell, Search, Menu } from 'lucide-react';
import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';

export default function WalletHeader() {
  const navigate = useNavigate();
  const [userName, setUserName] = useState('Người dùng');
  const [userAvatar, setUserAvatar] = useState('');

  useEffect(() => {
    const userInfo = localStorage.getItem('userInfo');
    if (userInfo) {
      const user = JSON.parse(userInfo);
      setUserName(user.name || 'Người dùng');
      setUserAvatar(user.avatar || '');
    }
  }, []);

  const getInitials = (name: string) => {
    return name
      .split(' ')
      .map(word => word[0])
      .join('')
      .toUpperCase()
      .slice(0, 2);
  };

  return (
    <div className="bg-gradient-to-r from-gray-800 to-black px-6 py-8 text-white">
      <div className="flex items-center justify-between">
        <div className="flex items-center space-x-4">
          <div className="w-14 h-14 bg-white/10 border border-white/20 rounded-full flex items-center justify-center font-bold text-sm overflow-hidden">
            {userAvatar ? (
              <img src={userAvatar} alt={userName} className="w-full h-full object-cover" />
            ) : (
              getInitials(userName)
            )}
          </div>
          <div>
            <div className="text-xl font-bold mb-1">HTD PAY</div>
            <div className="text-sm opacity-80">Chào mừng, {userName}</div>
          </div>
        </div>
        <div className="flex items-center space-x-5">
          <button aria-label="Tìm giao dịch" onClick={() => navigate('/history')}><Search className="w-6 h-6" /></button>
          <button aria-label="Thông báo" onClick={() => navigate('/notifications')}><Bell className="w-6 h-6" /></button>
          <button aria-label="Cài đặt" onClick={() => navigate('/settings')}><Menu className="w-6 h-6" /></button>
        </div>
      </div>
    </div>
  );
}

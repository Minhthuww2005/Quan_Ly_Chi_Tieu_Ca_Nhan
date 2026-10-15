import { useState, useEffect } from 'react';
import { ArrowLeft, Shield, Lock, Key, Eye, EyeOff, Fingerprint, Smartphone, AlertTriangle, CheckCircle2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../components/ui/dialog';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';

export default function SecurityPage() {
  const navigate = useNavigate();
  const [security, setSecurity] = useState({
    has2FA: false,
    hasPin: false,
    hasBiometric: false,
    lastPasswordChange: ''
  });
  const [loading, setLoading] = useState(true);
  const [showPasswordModal, setShowPasswordModal] = useState(false);
  const [showPinModal, setShowPinModal] = useState(false);
  const [showPasswordInput, setShowPasswordInput] = useState({
    old: false,
    new: false,
    confirm: false
  });
  
  const [passwordForm, setPasswordForm] = useState({
    oldPassword: '',
    newPassword: '',
    confirmPassword: ''
  });

  const [pinForm, setPinForm] = useState({
    oldPin: '',
    newPin: '',
    confirmPin: ''
  });

  useEffect(() => {
    fetchSecurity();
  }, []);

  const fetchSecurity = async () => {
    try {
      const userInfo = JSON.parse(localStorage.getItem('userInfo') || '{}');
      const response = await fetch(`/api/security/${userInfo.user_id}`);
      const data = await response.json();
      
      if (data.success) {
        setSecurity(data.security);
      }
    } catch (error) {
      console.error('Error fetching security:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleChangePassword = async () => {
    if (passwordForm.newPassword !== passwordForm.confirmPassword) {
      alert('Mật khẩu xác nhận không khớp');
      return;
    }

    try {
      const userInfo = JSON.parse(localStorage.getItem('userInfo') || '{}');
      const response = await fetch('/api/security/change-password', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          user_id: userInfo.user_id,
          old_password: passwordForm.oldPassword,
          new_password: passwordForm.newPassword
        })
      });

      const data = await response.json();
      if (data.success) {
        setShowPasswordModal(false);
        setPasswordForm({ oldPassword: '', newPassword: '', confirmPassword: '' });
        alert('Đổi mật khẩu thành công!');
        fetchSecurity();
      } else {
        alert(data.message);
      }
    } catch (error) {
      console.error('Error changing password:', error);
    }
  };

  const handleChangePin = async () => {
    if (pinForm.newPin !== pinForm.confirmPin) {
      alert('Mã PIN xác nhận không khớp');
      return;
    }

    if (pinForm.newPin.length !== 6 || !/^\d+$/.test(pinForm.newPin)) {
      alert('Mã PIN phải là 6 chữ số');
      return;
    }

    try {
      const userInfo = JSON.parse(localStorage.getItem('userInfo') || '{}');
      
      const endpoint = security.hasPin ? '/api/security/change-pin' : '/api/security/setup-pin';
      const body = security.hasPin 
        ? { user_id: userInfo.user_id, old_pin: pinForm.oldPin, new_pin: pinForm.newPin }
        : { user_id: userInfo.user_id, pin: pinForm.newPin };

      const response = await fetch(endpoint, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
      });

      const data = await response.json();
      if (data.success) {
        setShowPinModal(false);
        setPinForm({ oldPin: '', newPin: '', confirmPin: '' });
        alert(security.hasPin ? 'Đổi mã PIN thành công!' : 'Thiết lập mã PIN thành công!');
        fetchSecurity();
      } else {
        alert(data.message);
      }
    } catch (error) {
      console.error('Error changing PIN:', error);
    }
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
          <p className="mt-4 text-gray-600">Đang tải...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <div className="bg-gradient-to-r from-green-600 to-teal-600 text-white">
        <div className="max-w-7xl mx-auto px-4 py-6">
          <div className="flex items-center gap-4">
            <button onClick={() => navigate(-1)} className="p-2 hover:bg-white/10 rounded-lg">
              <ArrowLeft className="w-6 h-6" />
            </button>
            <div>
              <h1 className="text-2xl font-bold">Bảo mật</h1>
              <p className="text-green-100 text-sm mt-1">Cài đặt bảo mật tài khoản</p>
            </div>
          </div>
        </div>
      </div>

      <div className="max-w-3xl mx-auto px-4 py-8">
        {/* Security Status */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-100 p-6 mb-6">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-12 h-12 bg-green-100 rounded-lg flex items-center justify-center">
              <Shield className="w-6 h-6 text-green-600" />
            </div>
            <div>
              <h2 className="text-lg font-semibold text-gray-900">Trạng thái bảo mật</h2>
              <p className="text-sm text-gray-600">Thông tin cài đặt bảo mật của bạn</p>
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="flex items-center justify-between p-4 bg-gray-50 rounded-lg">
              <div className="flex items-center gap-3">
                <Key className="w-5 h-5 text-gray-600" />
                <span className="text-sm text-gray-700">Mã PIN giao dịch</span>
              </div>
              <span className={`px-3 py-1 rounded-full text-xs font-medium ${
                security.hasPin ? 'bg-green-100 text-green-700' : 'bg-orange-100 text-orange-700'
              }`}>
                {security.hasPin ? 'Đã thiết lập' : 'Cần thiết lập'}
              </span>
            </div>

            <div className="flex items-center justify-between p-4 bg-gray-50 rounded-lg">
              <div className="flex items-center gap-3">
                <Smartphone className="w-5 h-5 text-gray-600" />
                <span className="text-sm text-gray-700">Xác thực 2 yếu tố</span>
              </div>
              <span className="px-3 py-1 rounded-full text-xs font-medium bg-gray-100 text-gray-600">
                Sắp ra mắt
              </span>
            </div>

            <div className="flex items-center justify-between p-4 bg-gray-50 rounded-lg">
              <div className="flex items-center gap-3">
                <Fingerprint className="w-5 h-5 text-gray-600" />
                <span className="text-sm text-gray-700">Sinh trắc học</span>
              </div>
              <span className="px-3 py-1 rounded-full text-xs font-medium bg-gray-100 text-gray-600">
                Sắp ra mắt
              </span>
            </div>

            <div className="flex items-center justify-between p-4 bg-gray-50 rounded-lg">
              <div className="flex items-center gap-3">
                <Lock className="w-5 h-5 text-gray-600" />
                <span className="text-sm text-gray-700">Mã hóa dữ liệu</span>
              </div>
              <span className="px-3 py-1 rounded-full text-xs font-medium bg-green-100 text-green-700">
                <CheckCircle2 className="w-3 h-3 inline mr-1" />
                Đã bật
              </span>
            </div>
          </div>

          {security.lastPasswordChange && (
            <div className="mt-4 pt-4 border-t border-gray-200">
              <p className="text-sm text-gray-600">
                Lần đổi mật khẩu gần nhất: {new Date(security.lastPasswordChange).toLocaleDateString('vi-VN')}
              </p>
            </div>
          )}
        </div>

        {/* Security Tips */}
        <div className="bg-gradient-to-r from-blue-50 to-indigo-50 border border-blue-200 rounded-xl p-6 mb-6">
          <div className="flex items-start gap-3">
            <AlertTriangle className="w-5 h-5 text-blue-600 mt-1 flex-shrink-0" />
            <div>
              <h3 className="font-semibold text-gray-900 mb-2">Mẹo bảo mật tài khoản</h3>
              <ul className="space-y-2 text-sm text-gray-700">
                <li className="flex items-start gap-2">
                  <span className="text-blue-600 font-bold">•</span>
                  <span>Sử dụng mật khẩu mạnh, kết hợp chữ hoa, chữ thường, số và ký tự đặc biệt</span>
                </li>
                <li className="flex items-start gap-2">
                  <span className="text-blue-600 font-bold">•</span>
                  <span>Đổi mật khẩu định kỳ 3-6 tháng/lần</span>
                </li>
                <li className="flex items-start gap-2">
                  <span className="text-blue-600 font-bold">•</span>
                  <span>Không chia sẻ mã PIN với bất kỳ ai, kể cả nhân viên ngân hàng</span>
                </li>
                <li className="flex items-start gap-2">
                  <span className="text-blue-600 font-bold">•</span>
                  <span>Bật xác thực 2 yếu tố khi tính năng ra mắt</span>
                </li>
                <li className="flex items-start gap-2">
                  <span className="text-blue-600 font-bold">•</span>
                  <span>Kiểm tra định kỳ các giao dịch và hoạt động tài khoản</span>
                </li>
              </ul>
            </div>
          </div>
        </div>

        {/* Security Actions */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-100">
          <div className="p-6 border-b border-gray-100">
            <h2 className="text-lg font-semibold text-gray-900">Cài đặt bảo mật</h2>
          </div>

          <div className="divide-y divide-gray-100">
            {/* Change Password */}
            <div className="p-6 flex items-center justify-between hover:bg-gray-50 transition-colors">
              <div className="flex items-center gap-4">
                <div className="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center">
                  <Lock className="w-5 h-5 text-blue-600" />
                </div>
                <div>
                  <h3 className="font-semibold text-gray-900">Đổi mật khẩu</h3>
                  <p className="text-sm text-gray-600">Thay đổi mật khẩu đăng nhập</p>
                </div>
              </div>
              <Button onClick={() => setShowPasswordModal(true)} variant="outline">
                Đổi mật khẩu
              </Button>
            </div>

            {/* Change PIN */}
            <div className="p-6 flex items-center justify-between hover:bg-gray-50 transition-colors">
              <div className="flex items-center gap-4">
                <div className="w-10 h-10 bg-green-100 rounded-lg flex items-center justify-center">
                  <Key className="w-5 h-5 text-green-600" />
                </div>
                <div>
                  <h3 className="font-semibold text-gray-900">
                    {security.hasPin ? 'Đổi mã PIN' : 'Thiết lập mã PIN'}
                  </h3>
                  <p className="text-sm text-gray-600">
                    {security.hasPin ? 'Thay đổi mã PIN giao dịch' : 'Tạo mã PIN 6 số để xác thực giao dịch'}
                  </p>
                </div>
              </div>
              <Button onClick={() => setShowPinModal(true)} variant="outline" className="text-green-600 border-green-600 hover:bg-green-50">
                {security.hasPin ? 'Đổi PIN' : 'Thiết lập'}
              </Button>
            </div>
          </div>
        </div>
      </div>

      {/* Change Password Modal */}
      <Dialog open={showPasswordModal} onOpenChange={setShowPasswordModal}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Đổi mật khẩu</DialogTitle>
            <DialogDescription>
              Nhập mật khẩu hiện tại và mật khẩu mới
            </DialogDescription>
          </DialogHeader>
          
          <div className="space-y-4 py-4">
            <div>
              <Label htmlFor="oldPassword">Mật khẩu hiện tại</Label>
              <div className="relative">
                <Input
                  id="oldPassword"
                  type={showPasswordInput.old ? "text" : "password"}
                  value={passwordForm.oldPassword}
                  onChange={(e) => setPasswordForm({ ...passwordForm, oldPassword: e.target.value })}
                />
                <button
                  type="button"
                  onClick={() => setShowPasswordInput({ ...showPasswordInput, old: !showPasswordInput.old })}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-500"
                >
                  {showPasswordInput.old ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
            </div>

            <div>
              <Label htmlFor="newPassword">Mật khẩu mới</Label>
              <div className="relative">
                <Input
                  id="newPassword"
                  type={showPasswordInput.new ? "text" : "password"}
                  value={passwordForm.newPassword}
                  onChange={(e) => setPasswordForm({ ...passwordForm, newPassword: e.target.value })}
                />
                <button
                  type="button"
                  onClick={() => setShowPasswordInput({ ...showPasswordInput, new: !showPasswordInput.new })}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-500"
                >
                  {showPasswordInput.new ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
            </div>

            <div>
              <Label htmlFor="confirmPassword">Xác nhận mật khẩu mới</Label>
              <div className="relative">
                <Input
                  id="confirmPassword"
                  type={showPasswordInput.confirm ? "text" : "password"}
                  value={passwordForm.confirmPassword}
                  onChange={(e) => setPasswordForm({ ...passwordForm, confirmPassword: e.target.value })}
                />
                <button
                  type="button"
                  onClick={() => setShowPasswordInput({ ...showPasswordInput, confirm: !showPasswordInput.confirm })}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-500"
                >
                  {showPasswordInput.confirm ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setShowPasswordModal(false)}>Hủy</Button>
            <Button onClick={handleChangePassword}>Đổi mật khẩu</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Change/Setup PIN Modal */}
      <Dialog open={showPinModal} onOpenChange={setShowPinModal}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{security.hasPin ? 'Đổi mã PIN' : 'Thiết lập mã PIN'}</DialogTitle>
            <DialogDescription>
              {security.hasPin ? 'Nhập mã PIN hiện tại và mã PIN mới' : 'Tạo mã PIN 6 số'}
            </DialogDescription>
          </DialogHeader>
          
          <div className="space-y-4 py-4">
            {security.hasPin && (
              <div>
                <Label htmlFor="oldPin">Mã PIN hiện tại</Label>
                <Input
                  id="oldPin"
                  type="password"
                  maxLength={6}
                  value={pinForm.oldPin}
                  onChange={(e) => setPinForm({ ...pinForm, oldPin: e.target.value.replace(/\D/g, '') })}
                  placeholder="Nhập 6 chữ số"
                />
              </div>
            )}

            <div>
              <Label htmlFor="newPin">Mã PIN mới</Label>
              <Input
                id="newPin"
                type="password"
                maxLength={6}
                value={pinForm.newPin}
                onChange={(e) => setPinForm({ ...pinForm, newPin: e.target.value.replace(/\D/g, '') })}
                placeholder="Nhập 6 chữ số"
              />
            </div>

            <div>
              <Label htmlFor="confirmPin">Xác nhận mã PIN</Label>
              <Input
                id="confirmPin"
                type="password"
                maxLength={6}
                value={pinForm.confirmPin}
                onChange={(e) => setPinForm({ ...pinForm, confirmPin: e.target.value.replace(/\D/g, '') })}
                placeholder="Nhập lại 6 chữ số"
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setShowPinModal(false)}>Hủy</Button>
            <Button onClick={handleChangePin} className="bg-green-600 hover:bg-green-700">
              {security.hasPin ? 'Đổi PIN' : 'Thiết lập'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

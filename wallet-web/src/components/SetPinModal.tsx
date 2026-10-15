/**
 * Set PIN Modal Component
 * Allows users to set or change their 6-digit PIN for transactions
 */

import { useState } from 'react';
import { authAPI } from '../services/api';
import { Button } from './ui/button';
import { Input } from './ui/input';
import { Label } from './ui/label';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from './ui/dialog';
import { Lock, Eye, EyeOff } from 'lucide-react';

interface SetPinModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  userId: number;
  hasExistingPin?: boolean;
  onPinSet?: () => void;
}

export default function SetPinModal({ 
  open, 
  onOpenChange, 
  userId,
  hasExistingPin = false,
  onPinSet
}: SetPinModalProps) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);
  const [showPin, setShowPin] = useState(false);

  const [formData, setFormData] = useState({
    oldPin: '',
    newPin: '',
    confirmPin: '',
  });

  const validatePin = (pin: string): boolean => {
    return /^\d{6}$/.test(pin);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      // Validation
      if (hasExistingPin && !formData.oldPin) {
        setError('Vui lòng nhập mã PIN hiện tại');
        setLoading(false);
        return;
      }

      if (!formData.newPin || !formData.confirmPin) {
        setError('Vui lòng điền đầy đủ thông tin');
        setLoading(false);
        return;
      }

      if (!validatePin(formData.newPin)) {
        setError('Mã PIN phải là 6 chữ số');
        setLoading(false);
        return;
      }

      if (formData.newPin !== formData.confirmPin) {
        setError('Mã PIN xác nhận không khớp');
        setLoading(false);
        return;
      }

      // Call API (always use set-pin endpoint, backend handles both set and change)
      const data = await authAPI.setPin(
        userId,
        formData.newPin,
        hasExistingPin ? formData.oldPin : undefined
      );

      if (data.success) {
        setSuccess(true);
        setFormData({ oldPin: '', newPin: '', confirmPin: '' });
        
        // Update localStorage to reflect PIN status
        const userInfo = localStorage.getItem('userInfo');
        if (userInfo) {
          const user = JSON.parse(userInfo);
          user.hasPin = true;
          localStorage.setItem('userInfo', JSON.stringify(user));
        }

        setTimeout(() => {
          setSuccess(false);
          onOpenChange(false);
          if (onPinSet) onPinSet();
        }, 2000);
      }
    } catch (err: any) {
      setError(err.message || 'Đã xảy ra lỗi. Vui lòng thử lại.');
    } finally {
      setLoading(false);
    }
  };

  const handleClose = () => {
    setError('');
    setSuccess(false);
    setFormData({ oldPin: '', newPin: '', confirmPin: '' });
    onOpenChange(false);
  };

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-[425px]">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Lock className="w-5 h-5 text-blue-600" />
            {hasExistingPin ? 'Đổi mã PIN' : 'Thiết lập mã PIN'}
          </DialogTitle>
          <DialogDescription>
            {hasExistingPin 
              ? 'Thay đổi mã PIN giao dịch của bạn'
              : 'Tạo mã PIN 6 số để bảo mật giao dịch'}
          </DialogDescription>
        </DialogHeader>

        {success ? (
          <div className="space-y-4 py-4">
            <div className="bg-green-50 border border-green-200 rounded-lg p-4">
              <p className="text-green-800 text-center font-medium">
                ✅ {hasExistingPin ? 'Đổi mã PIN thành công!' : 'Thiết lập mã PIN thành công!'}
              </p>
              <p className="text-green-700 text-sm text-center mt-2">
                Bạn có thể sử dụng mã PIN mới để xác thực giao dịch
              </p>
            </div>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4 py-4">
            {error && (
              <div className="bg-red-50 border border-red-200 rounded-lg p-3">
                <p className="text-red-800 text-sm">{error}</p>
              </div>
            )}

            {/* Warning Note */}
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-3">
              <p className="text-blue-800 text-sm">
                <strong>Lưu ý:</strong> Mã PIN được dùng để xác thực giao dịch nạp tiền và chuyển tiền. 
                Vui lòng không chia sẻ mã PIN với bất kỳ ai.
              </p>
            </div>

            {/* Old PIN (only if changing) */}
            {hasExistingPin && (
              <div className="space-y-2">
                <Label htmlFor="oldPin">Mã PIN hiện tại</Label>
                <div className="relative">
                  <Input
                    id="oldPin"
                    type={showPin ? 'text' : 'password'}
                    placeholder="Nhập mã PIN hiện tại"
                    value={formData.oldPin}
                    onChange={(e) => {
                      const value = e.target.value.replace(/\D/g, '').slice(0, 6);
                      setFormData({ ...formData, oldPin: value });
                    }}
                    disabled={loading}
                    required
                    maxLength={6}
                    className="pr-10"
                  />
                  <button
                    type="button"
                    onClick={() => setShowPin(!showPin)}
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-500 hover:text-gray-700"
                  >
                    {showPin ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                </div>
              </div>
            )}

            {/* New PIN */}
            <div className="space-y-2">
              <Label htmlFor="newPin">Mã PIN mới</Label>
              <div className="relative">
                <Input
                  id="newPin"
                  type={showPin ? 'text' : 'password'}
                  placeholder="Nhập mã PIN 6 số"
                  value={formData.newPin}
                  onChange={(e) => {
                    const value = e.target.value.replace(/\D/g, '').slice(0, 6);
                    setFormData({ ...formData, newPin: value });
                  }}
                  disabled={loading}
                  required
                  maxLength={6}
                  className="pr-10 font-mono tracking-widest"
                />
                <button
                  type="button"
                  onClick={() => setShowPin(!showPin)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-500 hover:text-gray-700"
                >
                  {showPin ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
              <p className="text-xs text-gray-500">
                Phải là 6 chữ số (0-9)
              </p>
            </div>

            {/* Confirm PIN */}
            <div className="space-y-2">
              <Label htmlFor="confirmPin">Xác nhận mã PIN mới</Label>
              <Input
                id="confirmPin"
                type={showPin ? 'text' : 'password'}
                placeholder="Nhập lại mã PIN mới"
                value={formData.confirmPin}
                onChange={(e) => {
                  const value = e.target.value.replace(/\D/g, '').slice(0, 6);
                  setFormData({ ...formData, confirmPin: value });
                }}
                disabled={loading}
                required
                maxLength={6}
                className="font-mono tracking-widest"
              />
            </div>

            {/* Buttons */}
            <div className="flex gap-2 pt-2">
              <Button
                type="button"
                variant="outline"
                className="flex-1"
                onClick={handleClose}
                disabled={loading}
              >
                Hủy
              </Button>
              <Button
                type="submit"
                className="flex-1 bg-blue-600 hover:bg-blue-700"
                disabled={loading}
              >
                {loading ? (
                  <div className="flex items-center gap-2">
                    <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin"></div>
                    Đang xử lý...
                  </div>
                ) : (
                  hasExistingPin ? 'Đổi mã PIN' : 'Thiết lập PIN'
                )}
              </Button>
            </div>
          </form>
        )}
      </DialogContent>
    </Dialog>
  );
}

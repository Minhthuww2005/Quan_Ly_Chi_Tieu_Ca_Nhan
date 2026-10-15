/**
 * ForgotPasswordModal - Component cho chức năng quên mật khẩu
 * Gửi email reset password
 */

import { useState } from 'react';
import { X, Mail, CheckCircle } from 'lucide-react';
import { Button } from './ui/button';
import { Input } from './ui/input';
import { authAPI } from '../services/api';

interface ForgotPasswordModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export default function ForgotPasswordModal({ isOpen, onClose }: ForgotPasswordModalProps) {
  const [email, setEmail] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);
  const [resetToken, setResetToken] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError('');

    // Email validation
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(email)) {
      setError('Email không hợp lệ');
      setIsLoading(false);
      return;
    }

    try {
      const response = await authAPI.forgotPassword(email);

      console.log('Forgot password response:', response);
      
      // Save token for development
      if (response.resetToken) {
        setResetToken(response.resetToken);
      }

      setSuccess(true);
      
      // Tự động đóng sau 10 giây (để user copy token)
      setTimeout(() => {
        onClose();
        setSuccess(false);
        setEmail('');
        setResetToken('');
      }, 10000);

    } catch (err: any) {
      console.error('Forgot password error:', err);
      setError(err.message || 'Đã xảy ra lỗi. Vui lòng thử lại.');
    } finally {
      setIsLoading(false);
    }
  };

  if (!isOpen) return null;

  if (success) {
    return (
      <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center p-4 z-50">
        <div className="bg-white rounded-3xl p-8 w-full max-w-md text-center shadow-2xl">
          <div className="w-20 h-20 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-6">
            <CheckCircle className="w-10 h-10 text-green-600" />
          </div>
          <h2 className="text-3xl font-bold text-slate-800 mb-4">Thành công!</h2>
          <p className="text-slate-600 text-base leading-relaxed mb-6">
            Link đặt lại mật khẩu sẽ được gửi đến email <strong>{email}</strong>.
          </p>
          
          {/* Development mode: Show token */}
          {resetToken && (
            <div className="bg-yellow-50 border border-yellow-200 rounded-xl p-4 mb-4">
              <p className="text-xs font-semibold text-yellow-800 mb-2">
                🔧 DEVELOPMENT MODE - Reset Token:
              </p>
              <div className="bg-white rounded-lg p-3 border border-yellow-300">
                <code className="text-xs text-slate-700 break-all font-mono">
                  {resetToken}
                </code>
              </div>
              <button
                onClick={() => {
                  navigator.clipboard.writeText(resetToken);
                  alert('Token đã được copy!');
                }}
                className="mt-2 text-xs text-blue-600 hover:text-blue-700 font-semibold"
              >
                📋 Copy token
              </button>
              <p className="text-xs text-slate-500 mt-2">
                Sử dụng token này để test reset password
              </p>
            </div>
          )}

          <p className="text-sm text-slate-500">
            Vui lòng kiểm tra hộp thư và làm theo hướng dẫn.
          </p>
          
          <Button
            onClick={() => {
              onClose();
              setSuccess(false);
              setEmail('');
              setResetToken('');
            }}
            className="mt-6 bg-blue-600 hover:bg-blue-700 text-white px-8 py-3 rounded-xl"
          >
            Đóng
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center p-4 z-50">
      <div className="bg-white rounded-3xl p-8 w-full max-w-md relative shadow-2xl border border-slate-200/50">
        {/* Header */}
        <div className="flex items-center justify-between mb-8">
          <div>
            <h2 className="text-2xl font-bold text-slate-800">Quên mật khẩu?</h2>
            <p className="text-slate-600 mt-1">Nhập email để đặt lại mật khẩu của bạn</p>
          </div>
          <button 
            onClick={onClose}
            className="p-2 hover:bg-slate-100 rounded-full transition-colors"
          >
            <X className="w-5 h-5 text-slate-500" />
          </button>
        </div>

        {/* Form */}
        <form onSubmit={handleSubmit} className="space-y-6">
          {/* Email */}
          <div className="space-y-2">
            <label className="text-sm font-semibold text-slate-700">Email đã đăng ký</label>
            <div className="relative">
              <Mail className="w-5 h-5 text-slate-400 absolute left-4 top-1/2 transform -translate-y-1/2" />
              <Input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="example@email.com"
                className="pl-12 py-4 rounded-2xl border-slate-300 focus:border-blue-500 focus:ring-blue-500 text-base"
                required
              />
            </div>
          </div>

          {/* Error message */}
          {error && (
            <div className="bg-red-50 border border-red-200 text-red-600 px-4 py-3 rounded-xl text-sm">
              {error}
            </div>
          )}

          {/* Info box */}
          <div className="bg-blue-50 border border-blue-200 p-4 rounded-xl">
            <p className="text-sm text-blue-800">
              <strong>Lưu ý:</strong> Link đặt lại mật khẩu sẽ có hiệu lực trong 1 giờ. 
              Nếu không nhận được email, vui lòng kiểm tra thư mục spam.
            </p>
          </div>

          {/* Submit button */}
          <Button
            type="submit"
            disabled={isLoading}
            className="w-full bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-700 hover:to-indigo-700 text-white font-semibold py-4 rounded-2xl transition-all duration-200 shadow-lg"
          >
            {isLoading ? (
              <div className="flex items-center justify-center">
                <div className="w-5 h-5 border-2 border-white/30 border-t-white rounded-full animate-spin mr-2"></div>
                Đang gửi...
              </div>
            ) : (
              'Gửi link đặt lại mật khẩu'
            )}
          </Button>
        </form>

        {/* Back to login */}
        <div className="text-center mt-6">
          <button 
            type="button" 
            onClick={onClose}
            className="text-blue-600 hover:text-blue-700 font-semibold"
          >
            ← Quay lại đăng nhập
          </button>
        </div>
      </div>
    </div>
  );
}

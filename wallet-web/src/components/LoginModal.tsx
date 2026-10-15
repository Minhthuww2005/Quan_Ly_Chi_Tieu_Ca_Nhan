import { useState } from 'react';
import { X, Eye, EyeOff, User, Lock } from 'lucide-react';
import { GoogleLogin, type CredentialResponse } from '@react-oauth/google';
import { Button } from './ui/button';
import { Input } from './ui/input';
import { useNavigate } from 'react-router-dom';
import { authAPI } from '../services/api';
import { useAuthConfiguration } from '../services/auth-config';

interface LoginModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSwitchToRegister?: () => void;
  onForgotPassword?: () => void;
}

export default function LoginModal({ isOpen, onClose, onSwitchToRegister, onForgotPassword }: LoginModalProps) {
  const [identifier, setIdentifier] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const navigate = useNavigate();
  const config = useAuthConfiguration();

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError('');

    try {
      const data = await authAPI.login({ identifier, password });

      // Lưu token và thông tin user vào localStorage
      localStorage.setItem('token', data.token);
      localStorage.setItem('isLoggedIn', 'true');
      localStorage.setItem('userInfo', JSON.stringify(data.user));
      
      // Chuyển hướng đến trang chính
      navigate('/wallet');
      onClose();
    } catch (err: any) {
      setError(err.message || 'Đã xảy ra lỗi. Vui lòng thử lại.');
    } finally {
      setIsLoading(false);
    }
  };

  /**
   * Xử lý Google Sign-In
   * Nhận credential từ Google và gửi lên backend để xác thực
   */
  const handleGoogleSuccess = async (credentialResponse: CredentialResponse) => {
    setIsLoading(true);
    setError('');

    try {
      if (!credentialResponse.credential) {
        throw new Error('Không nhận được credential từ Google');
      }

      // Gửi credential (Google ID token) lên backend
      const data = await authAPI.googleLogin({
        credential: credentialResponse.credential
      });

      // Lưu token và thông tin user vào localStorage
      localStorage.setItem('token', data.token);
      localStorage.setItem('isLoggedIn', 'true');
      localStorage.setItem('userInfo', JSON.stringify(data.user));
      
      // Chuyển hướng đến trang chính
      navigate('/wallet');
      onClose();
    } catch (err: any) {
      console.error('Google Sign-In error:', err);
      setError(err.message || 'Đăng nhập Google thất bại. Vui lòng thử lại.');
    } finally {
      setIsLoading(false);
    }
  };

  /**
   * Xử lý lỗi Google Sign-In
   */
  const handleGoogleError = () => {
    setError('Đăng nhập Google thất bại. Vui lòng thử lại.');
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center p-4 z-50">
      <div className="bg-white rounded-3xl p-8 w-full max-w-md relative shadow-2xl border border-slate-200/50">
        {/* Header */}
        <div className="flex items-center justify-between mb-8">
          <div>
            <h2 className="text-2xl font-bold text-slate-800">Chào mừng trở lại</h2>
            <p className="text-slate-600 mt-1">Đăng nhập vào tài khoản HTD PAY của bạn</p>
          </div>
          <button 
            onClick={onClose}
            className="p-2 hover:bg-slate-100 rounded-full transition-colors"
          >
            <X className="w-5 h-5 text-slate-500" />
          </button>
        </div>

        {/* Form */}
        <form onSubmit={handleLogin} className="space-y-6">
          {/* Identifier (Email or Username) */}
          <div className="space-y-2">
            <label className="text-sm font-semibold text-slate-700">Email hoặc số điện thoại</label>
            <div className="relative">
              <User className="w-5 h-5 text-slate-400 absolute left-4 top-1/2 transform -translate-y-1/2" />
              <Input
                type="text"
                value={identifier}
                onChange={(e) => setIdentifier(e.target.value)}
                placeholder="Nhập email hoặc số điện thoại"
                className="pl-12 py-4 rounded-2xl border-slate-300 focus:border-blue-500 focus:ring-blue-500 text-base"
                required
              />
            </div>
          </div>

          {/* Password */}
          <div className="space-y-2">
            <label className="text-sm font-semibold text-slate-700">Mật khẩu</label>
            <div className="relative">
              <Lock className="w-5 h-5 text-slate-400 absolute left-4 top-1/2 transform -translate-y-1/2" />
              <Input
                type={showPassword ? 'text' : 'password'}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Nhập mật khẩu"
                className="pl-12 pr-12 py-4 rounded-2xl border-slate-300 focus:border-blue-500 focus:ring-blue-500 text-base"
                required
              />
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                className="absolute right-4 top-1/2 transform -translate-y-1/2 text-slate-400 hover:text-slate-600"
              >
                {showPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
              </button>
            </div>
          </div>

          {/* Error message */}
          {error && (
            <div className="bg-red-50 border border-red-200 text-red-600 px-4 py-3 rounded-xl text-sm">
              {error}
            </div>
          )}

          {/* Remember me & Forgot password */}
          <div className="flex items-center justify-between text-sm">
            <label className="flex items-center space-x-3">
              <input type="checkbox" className="rounded border-slate-300 text-blue-600 focus:ring-blue-500" />
              <span className="text-slate-600 font-medium">Ghi nhớ đăng nhập</span>
            </label>
            <button 
              type="button" 
              onClick={() => {
                onClose();
                onForgotPassword?.();
              }}
              className="text-blue-600 hover:text-blue-700 font-semibold"
            >
              Quên mật khẩu?
            </button>
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
                Đang đăng nhập...
              </div>
            ) : (
              'Đăng nhập'
            )}
          </Button>
        </form>

        {/* Divider */}
        <div className="my-8 flex items-center">
          <div className="flex-1 border-t border-slate-200"></div>
          <span className="px-4 text-sm text-slate-500 font-medium">Hoặc đăng nhập với</span>
          <div className="flex-1 border-t border-slate-200"></div>
        </div>

        {/* Social login */}
        <div className="space-y-3">
          {/* Google Sign-In Button */}
          {config.googleClientId && <div className="w-full flex justify-center">
            <GoogleLogin
              onSuccess={handleGoogleSuccess}
              onError={handleGoogleError}
              useOneTap={false}
              size="large"
              text="continue_with"
              shape="rectangular"
              theme="outline"
              logo_alignment="left"
              width="100%"
            />
          </div>}
        </div>

        {/* Sign up link */}
        <div className="text-center mt-8">
          <span className="text-slate-600">Chưa có tài khoản? </span>
          <button 
            type="button" 
            onClick={() => {
              onClose();
              onSwitchToRegister?.();
            }}
            className="text-blue-600 hover:text-blue-700 font-semibold"
          >
            Đăng ký ngay
          </button>
        </div>
      </div>
    </div>
  );
}

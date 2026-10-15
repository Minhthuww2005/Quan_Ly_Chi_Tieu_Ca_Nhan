/**
 * AuthModals - Component quản lý Login, Register và ForgotPassword Modals
 * Xử lý chuyển đổi giữa các modal authentication
 */

import { useState } from 'react';
import LoginModal from './LoginModal';
import RegisterModal from './RegisterModal';
import ForgotPasswordModal from './ForgotPasswordModal';

interface AuthModalsProps {
  initialModal?: 'login' | 'register' | 'forgot-password' | null;
  onClose?: () => void;
}

export default function AuthModals({ initialModal = null, onClose }: AuthModalsProps) {
  const [activeModal, setActiveModal] = useState<'login' | 'register' | 'forgot-password' | null>(initialModal);

  const handleClose = () => {
    setActiveModal(null);
    onClose?.();
  };

  const handleSwitchToRegister = () => {
    setActiveModal('register');
  };

  const handleSwitchToLogin = () => {
    setActiveModal('login');
  };

  const handleSwitchToForgotPassword = () => {
    setActiveModal('forgot-password');
  };

  return (
    <>
      <LoginModal
        isOpen={activeModal === 'login'}
        onClose={handleClose}
        onSwitchToRegister={handleSwitchToRegister}
        onForgotPassword={handleSwitchToForgotPassword}
      />
      <RegisterModal
        isOpen={activeModal === 'register'}
        onClose={handleClose}
        onSwitchToLogin={handleSwitchToLogin}
      />
      <ForgotPasswordModal
        isOpen={activeModal === 'forgot-password'}
        onClose={() => {
          setActiveModal('login');
        }}
      />
    </>
  );
}

// Hook để sử dụng auth modals
export function useAuthModals() {
  const [activeModal, setActiveModal] = useState<'login' | 'register' | 'forgot-password' | null>(null);

  const openLogin = () => setActiveModal('login');
  const openRegister = () => setActiveModal('register');
  const openForgotPassword = () => setActiveModal('forgot-password');
  const close = () => setActiveModal(null);

  return {
    activeModal,
    openLogin,
    openRegister,
    openForgotPassword,
    close,
  };
}

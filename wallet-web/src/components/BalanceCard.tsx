import { Eye, EyeOff, ArrowUpRight, ArrowDownLeft } from 'lucide-react';
import { useState, useEffect } from 'react';
import { Button } from './ui/button';
import { WalletTransferModal } from './EnhancedTransferModals';
import BankTopupModal from './BankTopupModal';
import SetPinModal from './SetPinModal';
import { walletAPI } from '../services/api';
import { WALLET_CHANGED, notifyWalletChanged } from '../services/walletEvents';

export default function BalanceCard() {
  const [showBalance, setShowBalance] = useState(() => !JSON.parse(localStorage.getItem('userInfo') || '{}').settings?.hideBalance);
  const [transferModalOpen, setTransferModalOpen] = useState(false);
  const [bankTopupModalOpen, setBankTopupModalOpen] = useState(false);
  const [showSetPinModal, setShowSetPinModal] = useState(false);
  const [balance, setBalance] = useState(0);
  const [walletId, setWalletId] = useState<string>('');
  const [loading, setLoading] = useState(true);

  const handleSetPinRequired = () => {
    setBankTopupModalOpen(false);
    setShowSetPinModal(true);
  };

  // Fetch wallet data from API
  useEffect(() => {
    const fetchWallet = async () => {
      try {
        const userInfo = localStorage.getItem('userInfo');
        if (!userInfo) {
          console.log('No user info found');
          setLoading(false);
          return;
        }

        const user = JSON.parse(userInfo);
        if (!user.user_id) {
          console.error('Invalid user data');
          setLoading(false);
          return;
        }

        const response = await walletAPI.getWallet(user.user_id);
        
        if (response.success && response.wallet) {
          setBalance(response.wallet.balance || 0);
          setWalletId(response.wallet.wallet_id?.toString() || '');
        }
      } catch (error: any) {
        console.error('Error fetching wallet:', error);
        // Không hiển thị alert để tránh làm phiền user
      } finally {
        setLoading(false);
      }
    };

    fetchWallet();
    
    // Reload wallet sau mỗi 30s để cập nhật số dư, và ngay sau mỗi giao dịch
    const interval = setInterval(fetchWallet, 30000);
    window.addEventListener(WALLET_CHANGED, fetchWallet);
    return () => {
      clearInterval(interval);
      window.removeEventListener(WALLET_CHANGED, fetchWallet);
    };
  }, []);

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('vi-VN').format(amount) + ' VNĐ';
  };

  const handleTransferClick = () => {
    setTransferModalOpen(true);
  };

  const handleTopupClick = () => {
    setBankTopupModalOpen(true);
  };

  return (
    <>
      {/* Account Balance Card */}
      <div className="bg-gradient-to-br from-pink-50 via-white to-rose-50 mx-4 -mt-6 rounded-2xl p-6 shadow-xl border border-pink-100 relative z-10 mb-6">
        <div className="flex items-center justify-between mb-2">
          <div className="text-sm text-gray-600 font-medium">Tài khoản thanh toán</div>
          <button 
            onClick={() => setShowBalance(!showBalance)}
            className="p-2 hover:bg-gray-100 rounded-full transition-colors"
          >
            {showBalance ? <Eye className="w-4 h-4 text-gray-600" /> : <EyeOff className="w-4 h-4 text-gray-600" />}
          </button>
        </div>
        
        <div className="text-xs text-gray-500 mb-4">
          {walletId ? `STK: ${walletId}` : 'Đang tải...'}
        </div>
        
        <div className="text-3xl font-bold text-gray-900 mb-6">
          {loading ? (
            <span className="text-gray-400">Đang tải...</span>
          ) : showBalance ? (
            formatCurrency(balance)
          ) : (
            '••••••••• VNĐ'
          )}
        </div>
        
        <div className="flex space-x-3">
          <Button 
            onClick={handleTransferClick}
            variant="outline" 
            className="flex-1 bg-white hover:bg-gray-50 text-gray-700 border-gray-300 rounded-xl py-3 font-medium"
          >
            <ArrowUpRight className="w-4 h-4 mr-2" />
            Chuyển tiền
          </Button>
          <Button 
            onClick={handleTopupClick}
            variant="outline" 
            className="flex-1 bg-white hover:bg-gray-50 text-gray-700 border-gray-300 rounded-xl py-3 font-medium"
          >
            <ArrowDownLeft className="w-4 h-4 mr-2" />
            Nạp tiền
          </Button>
        </div>
      </div>

      <WalletTransferModal
        isOpen={transferModalOpen}
        onClose={() => setTransferModalOpen(false)}
        userBalance={balance}
        onSuccess={notifyWalletChanged}
      />

      <BankTopupModal
        isOpen={bankTopupModalOpen}
        onClose={() => setBankTopupModalOpen(false)}
        onSetPinRequired={handleSetPinRequired}
      />

      <SetPinModal
        open={showSetPinModal}
        onOpenChange={setShowSetPinModal}
        userId={JSON.parse(localStorage.getItem('userInfo') || '{}').user_id || 0}
        hasExistingPin={false}
      />
    </>
  );
}

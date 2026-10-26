import { useState, useEffect } from 'react';
import { quickActions } from '../data/mockData';
import { 
  ArrowLeftRight, 
  Receipt, 
  Smartphone, 
  PiggyBank, 
  Banknote, 
  Shield, 
  TrendingUp, 
  Clock,
  CreditCard
} from 'lucide-react';
import TransferModal from './TransferModal';
import ServiceModal from './ServiceModals';
import BankTopupModal from './BankTopupModal';
import SetPinModal from './SetPinModal';
import { WalletTransferModal, BankTransferModal, PhoneTopupModal, BillPaymentModal } from './EnhancedTransferModals';
import { useNavigate } from 'react-router-dom';
import { WALLET_CHANGED, notifyWalletChanged } from '../services/walletEvents';

export default function QuickActions() {
  const navigate = useNavigate();
  const [transferModalOpen, setTransferModalOpen] = useState(false);
  const [transferType, setTransferType] = useState<'transfer' | 'topup' | null>(null);
  const [serviceModalOpen, setServiceModalOpen] = useState(false);
  const [serviceType, setServiceType] = useState<'savings' | 'loan' | 'insurance' | 'investment' | null>(null);
  const [bankTopupModalOpen, setBankTopupModalOpen] = useState(false);
  const [showSetPinModal, setShowSetPinModal] = useState(false);
  
  // Enhanced modals state
  const [showWalletTransfer, setShowWalletTransfer] = useState(false);
  const [showBankTransfer, setShowBankTransfer] = useState(false);
  const [showPhoneTopup, setShowPhoneTopup] = useState(false);
  const [showBillPayment, setShowBillPayment] = useState(false);
  const [userBalance, setUserBalance] = useState(0);

  // Load wallet balance
  useEffect(() => {
    const loadBalance = async () => {
      try {
        const userInfo = localStorage.getItem('userInfo');
        if (!userInfo) return;
        
        const user = JSON.parse(userInfo);
        const response = await fetch(`/api/wallet/${user.user_id}`);
        const data = await response.json();
        
        if (data.success) {
          setUserBalance(data.wallet.balance);
        }
      } catch (error) {
        console.error('Error loading balance:', error);
      }
    };
    
    loadBalance();
    window.addEventListener(WALLET_CHANGED, loadBalance);
    return () => window.removeEventListener(WALLET_CHANGED, loadBalance);
  }, []);

  const handleSetPinRequired = () => {
    setBankTopupModalOpen(false);
    setShowSetPinModal(true);
  };

  // Balance card, history and lists refresh themselves; no page reload.
  const handleTransactionSuccess = () => notifyWalletChanged();

  const getIcon = (title: string) => {
    switch (title) {
      case 'Chuyển khoản':
        return <ArrowLeftRight className="w-5 h-5 text-white" />;
      case 'Thanh toán hóa đơn':
        return <Receipt className="w-5 h-5 text-white" />;
      case 'Nạp tiền điện thoại':
        return <Smartphone className="w-5 h-5 text-white" />;
      case 'Nạp tiền':
        return <CreditCard className="w-5 h-5 text-white" />;
      case 'Chuyển đến ngân hàng':
        return <CreditCard className="w-5 h-5 text-white" />;
      case 'Mở sổ tiết kiệm':
        return <PiggyBank className="w-5 h-5 text-white" />;
      case 'Vay tín dụng':
        return <Banknote className="w-5 h-5 text-white" />;
      case 'Mua bảo hiểm':
        return <Shield className="w-5 h-5 text-white" />;
      case 'Đầu tư chứng khoán':
        return <TrendingUp className="w-5 h-5 text-white" />;
      case 'Lịch sử giao dịch':
        return <Clock className="w-5 h-5 text-white" />;
      default:
        return <div className="w-5 h-5 bg-white/20 rounded"></div>;
    }
  };

  const handleActionClick = (title: string) => {
    switch (title) {
      case 'Chuyển khoản':
        setShowWalletTransfer(true);
        break;
      case 'Chuyển đến ngân hàng':
        setShowBankTransfer(true);
        break;
      case 'Nạp tiền điện thoại':
        setShowPhoneTopup(true);
        break;
      case 'Thanh toán hóa đơn':
        setShowBillPayment(true);
        break;
      case 'Nạp tiền':
        setBankTopupModalOpen(true);
        break;
      case 'Mở sổ tiết kiệm':
        setServiceType('savings');
        setServiceModalOpen(true);
        break;
      case 'Vay tín dụng':
        setServiceType('loan');
        setServiceModalOpen(true);
        break;
      case 'Mua bảo hiểm':
        setServiceType('insurance');
        setServiceModalOpen(true);
        break;
      case 'Đầu tư chứng khoán':
        setServiceType('investment');
        setServiceModalOpen(true);
        break;
      case 'Lịch sử giao dịch':
        navigate('/history');
        break;
      default:
        break;
    }
  };

  return (
    <>
      <div className="px-4 py-6">
        <button className="mb-4 w-full rounded-xl bg-blue-700 p-4 text-white font-semibold" onClick={() => navigate('/payments')}>Nạp ví / Thanh toán qua VNPay sandbox</button>
        <h2 className="text-lg font-semibold text-gray-900 mb-4">Dịch vụ ngân hàng</h2>
        <div className="grid grid-cols-4 gap-3">
          {quickActions.map((action) => (
            <button
              key={action.id}
              onClick={() => handleActionClick(action.title)}
              className="flex flex-col items-center p-4 rounded-xl bg-white hover:bg-gray-50 border border-gray-200 hover:border-gray-300 transition-all duration-200 shadow-sm hover:shadow-md group"
            >
              <div className={`w-12 h-12 ${action.color} rounded-xl flex items-center justify-center mb-3 group-hover:scale-105 transition-transform`}>
                {getIcon(action.title)}
              </div>
              <span className="text-xs text-gray-700 text-center leading-tight font-medium">
                {action.title}
              </span>
            </button>
          ))}
        </div>
      </div>

      <TransferModal 
        isOpen={transferModalOpen}
        onClose={() => {
          setTransferModalOpen(false);
          setTransferType(null);
        }}
        type={transferType}
      />

      <ServiceModal
        isOpen={serviceModalOpen}
        onClose={() => {
          setServiceModalOpen(false);
          setServiceType(null);
        }}
        type={serviceType}
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

      {/* Enhanced MoMo-like Transfer Modals */}
      <WalletTransferModal
        isOpen={showWalletTransfer}
        onClose={() => setShowWalletTransfer(false)}
        userBalance={userBalance}
        onSuccess={handleTransactionSuccess}
      />
      
      <BankTransferModal
        isOpen={showBankTransfer}
        onClose={() => setShowBankTransfer(false)}
        userBalance={userBalance}
        onSuccess={handleTransactionSuccess}
      />
      
      <PhoneTopupModal
        isOpen={showPhoneTopup}
        onClose={() => setShowPhoneTopup(false)}
        userBalance={userBalance}
        onSuccess={handleTransactionSuccess}
      />
      
      <BillPaymentModal
        isOpen={showBillPayment}
        onClose={() => setShowBillPayment(false)}
        userBalance={userBalance}
        onSuccess={handleTransactionSuccess}
      />
    </>
  );
}

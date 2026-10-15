import { useState, useEffect } from 'react';
import { ArrowLeft, Phone, Mail, Shield, Settings, CreditCard, PiggyBank, TrendingUp, FileText, LogOut, Edit, Eye, EyeOff, ShieldCheck } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../components/ui/button';
import { Card, CardContent } from '../components/ui/card';
import { Avatar, AvatarFallback, AvatarImage } from '../components/ui/avatar';
import { Separator } from '../components/ui/separator';
import BottomNavigation from '../components/BottomNavigation';
import { walletAPI, cardAPI } from '../services/api';
import KYCVerificationModal from '../components/KYCVerificationModal';
import KYCVerifiedModal from '../components/KYCVerifiedModal';
import axios from 'axios';

export default function ProfilePage() {
  const navigate = useNavigate();
  const [showBalance, setShowBalance] = useState(true);
  const [userInfo, setUserInfo] = useState<any>(null);
  const [balance, setBalance] = useState(0);
  const [walletId, setWalletId] = useState('');
  const [cardCount, setCardCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [showKYCModal, setShowKYCModal] = useState(false);
  const [showKYCVerifiedModal, setShowKYCVerifiedModal] = useState(false);
  const [kycData, setKycData] = useState<any>(null);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const storedUserInfo = localStorage.getItem('userInfo');
        if (!storedUserInfo) {
          navigate('/');
          return;
        }

        const user = JSON.parse(storedUserInfo);
        setUserInfo(user);

        // Fetch wallet
        const walletResponse = await walletAPI.getWallet(user.user_id);
        if (walletResponse.success && walletResponse.wallet) {
          setBalance(walletResponse.wallet.balance || 0);
          setWalletId(walletResponse.wallet.wallet_id?.toString() || '');
        }

        // Fetch cards count
        const cardsResponse = await cardAPI.getCards(user.user_id);
        if (cardsResponse.success) {
          setCardCount(cardsResponse.cards?.length || 0);
        }

        // Fetch KYC status
        try {
          const kycResponse = await axios.get(`/api/kyc/status/${user.user_id}`);
          if (kycResponse.data.success && kycResponse.data.kyc) {
            setKycData(kycResponse.data.kyc);
          }
        } catch (error) {
          console.log('No KYC data found or error fetching:', error);
        }
      } catch (error) {
        console.error('Error fetching profile data:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchData();
  }, [navigate]);

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('vi-VN').format(amount) + ' VNĐ';
  };

  const handleLogout = () => {
    localStorage.removeItem('isLoggedIn');
    localStorage.removeItem('token');
    localStorage.removeItem('userInfo');
    navigate('/');
  };

  const getInitials = (name: string) => {
    if (!name) return 'U';
    return name
      .split(' ')
      .map(word => word[0])
      .join('')
      .toUpperCase()
      .slice(0, 2);
  };

  const menuItems = [
    {
      icon: ShieldCheck,
      title: 'Xác thực KYC',
      description: kycData?.status === 'verified' 
        ? 'Đã xác minh - Level ' + (kycData?.verification_level || 1)
        : kycData?.status === 'submitted'
        ? 'Đang chờ xác minh'
        : 'Xác minh danh tính - Mở khóa tính năng',
      action: () => {
        if (kycData?.status === 'verified' || kycData?.status === 'submitted') {
          setShowKYCVerifiedModal(true);
        } else {
          setShowKYCModal(true);
        }
      },
      badge: kycData?.status === 'verified' ? 'ĐÃ XÁC MINH' : kycData?.status === 'submitted' ? 'CHỜ DUYỆT' : 'MỚI',
      badgeColor: kycData?.status === 'verified' ? 'bg-green-500' : kycData?.status === 'submitted' ? 'bg-yellow-500' : 'bg-blue-500'
    },
    {
      icon: CreditCard,
      title: 'Quản lý thẻ',
      description: `${cardCount} thẻ đang hoạt động`,
      action: () => navigate('/cards')
    },
    {
      icon: PiggyBank,
      title: 'Sổ tiết kiệm',
      description: 'Quản lý tiết kiệm',
      action: () => navigate('/savings')
    },
    {
      icon: TrendingUp,
      title: 'Đầu tư',
      description: 'Danh mục đầu tư',
      action: () => navigate('/investments')
    },
    {
      icon: FileText,
      title: 'Hợp đồng vay',
      description: 'Quản lý khoản vay',
      action: () => navigate('/loans')
    },
    {
      icon: Shield,
      title: 'Bảo mật',
      description: 'Cài đặt bảo mật tài khoản',
      action: () => navigate('/security')
    },
    {
      icon: Settings,
      title: 'Cài đặt',
      description: 'Tùy chỉnh ứng dụng',
      action: () => navigate('/settings')
    },
    { icon: Shield, title: 'Bảo hiểm', description: 'Quản lý và gia hạn hợp đồng', action: () => navigate('/insurance') },
    { icon: CreditCard, title: 'VNPay sandbox', description: 'Nạp ví và thanh toán qua cổng kiểm thử', action: () => navigate('/payments') }
  ];

  if (loading || !userInfo) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 border-4 border-blue-600 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
          <div className="text-gray-600">Đang tải...</div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-50 pb-24 overflow-y-auto">
      {/* Header */}
      <div className="bg-gradient-to-r from-gray-800 to-black px-6 py-8 text-white sticky top-0 z-20">
        <div className="flex items-center justify-between">
          <div className="flex items-center space-x-4">
            <button 
              onClick={() => navigate('/wallet')}
              className="p-2 hover:bg-white/10 rounded-full transition-colors"
            >
              <ArrowLeft className="w-6 h-6" />
            </button>
            <div>
              <div className="text-xl font-bold">Cá nhân</div>
              <div className="text-sm opacity-80">Thông tin tài khoản</div>
            </div>
          </div>
          <button 
            onClick={() => setShowBalance(!showBalance)}
            className="p-2 hover:bg-white/10 rounded-full transition-colors"
          >
            {showBalance ? <Eye className="w-6 h-6" /> : <EyeOff className="w-6 h-6" />}
          </button>
        </div>
      </div>

      {/* Scrollable Content */}
      <div className="flex-1 overflow-y-auto">
        {/* Profile Info */}
        <div className="px-4 -mt-6 relative z-10">
          <Card className="shadow-xl">
            <CardContent className="p-6">
              <div className="flex items-center space-x-4 mb-6">
                <Avatar className="w-16 h-16">
                  {userInfo.avatar ? (
                    <AvatarImage src={userInfo.avatar} alt={userInfo.name} />
                  ) : null}
                  <AvatarFallback className="bg-gray-700 text-white text-lg font-bold">
                    {getInitials(userInfo.name)}
                  </AvatarFallback>
                </Avatar>
                <div className="flex-1">
                  <div className="flex items-center space-x-2 mb-1">
                    <h2 className="text-xl font-bold text-gray-900">{userInfo.name}</h2>
                    <Button variant="ghost" size="sm">
                      <Edit className="w-4 h-4" />
                    </Button>
                  </div>
                  <div className="text-sm text-gray-600 space-y-1">
                    {userInfo.phone && (
                      <div className="flex items-center space-x-2">
                        <Phone className="w-4 h-4" />
                        <span>{userInfo.phone}</span>
                      </div>
                    )}
                    <div className="flex items-center space-x-2">
                      <Mail className="w-4 h-4" />
                      <span>{userInfo.email}</span>
                    </div>
                  </div>
                </div>
              </div>

              <Separator className="mb-6" />

              {/* Account Summary */}
              <div className="grid grid-cols-2 gap-4 mb-6">
                <div className="text-center p-4 bg-gray-50 rounded-lg">
                  <div className="text-sm text-gray-600 mb-1">Tài khoản chính</div>
                  <div className="font-bold text-gray-900">
                    {showBalance ? formatCurrency(balance) : '••••••••• VNĐ'}
                  </div>
                </div>
                <div className="text-center p-4 bg-gray-50 rounded-lg">
                  <div className="text-sm text-gray-600 mb-1">Số tài khoản</div>
                  <div className="font-mono text-gray-900">
                    {showBalance ? walletId : '••••••••••'}
                  </div>
                </div>
              </div>

              {/* Financial Summary */}
              <div className="grid grid-cols-3 gap-3">
                <div className="text-center p-3 bg-green-50 rounded-lg">
                  <PiggyBank className="w-5 h-5 text-green-600 mx-auto mb-1" />
                  <div className="text-xs text-green-600 mb-1">Tiết kiệm</div>
                  <div className="text-sm font-semibold text-green-700">
                    {showBalance ? '0 VNĐ' : '•••••'}
                  </div>
                </div>
                <div className="text-center p-3 bg-blue-50 rounded-lg">
                  <TrendingUp className="w-5 h-5 text-blue-600 mx-auto mb-1" />
                  <div className="text-xs text-blue-600 mb-1">Đầu tư</div>
                  <div className="text-sm font-semibold text-blue-700">
                    {showBalance ? '0 VNĐ' : '•••••'}
                  </div>
                </div>
                <div className="text-center p-3 bg-red-50 rounded-lg">
                  <FileText className="w-5 h-5 text-red-600 mx-auto mb-1" />
                  <div className="text-xs text-red-600 mb-1">Nợ vay</div>
                  <div className="text-sm font-semibold text-red-700">
                    {showBalance ? '0 VNĐ' : '•••••'}
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Menu Items */}
        <div className="px-4 py-6 space-y-3">
          {menuItems.map((item, index) => {
            const Icon = item.icon;
            return (
              <Card key={index} className="hover:shadow-md transition-shadow cursor-pointer" onClick={item.action}>
                <CardContent className="p-4">
                  <div className="flex items-center space-x-4">
                    <div className="w-12 h-12 bg-gray-100 rounded-xl flex items-center justify-center">
                      <Icon className="w-6 h-6 text-gray-600" />
                    </div>
                    <div className="flex-1">
                      <div className="font-medium text-gray-900 mb-1 flex items-center gap-2">
                        {item.title}
                        {item.badge && (
                          <span className={`text-xs text-white px-2 py-0.5 rounded-full ${item.badgeColor}`}>
                            {item.badge}
                          </span>
                        )}
                      </div>
                      <div className="text-sm text-gray-500">{item.description}</div>
                    </div>
                    <div className="text-gray-400">
                      <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                      </svg>
                    </div>
                  </div>
                </CardContent>
              </Card>
            );
          })}

          {/* Logout */}
          <Card 
            className="border-red-200 hover:shadow-md transition-shadow cursor-pointer" 
            onClick={handleLogout}
          >
            <CardContent className="p-4">
              <div className="flex items-center space-x-4">
                <div className="w-12 h-12 bg-red-100 rounded-xl flex items-center justify-center">
                  <LogOut className="w-6 h-6 text-red-600" />
                </div>
                <div className="flex-1">
                  <div className="font-medium text-red-600">Đăng xuất</div>
                  <div className="text-sm text-gray-500">Thoát khỏi tài khoản</div>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>

      <BottomNavigation />

      {/* KYC Verification Modal */}
      <KYCVerificationModal
        open={showKYCModal}
        onOpenChange={setShowKYCModal}
        userId={userInfo?.user_id || 0}
        onKYCSubmitted={async () => {
          // Reload KYC data after submission
          try {
            const kycResponse = await axios.get(`/api/kyc/status/${userInfo.user_id}`);
            if (kycResponse.data.success && kycResponse.data.kyc) {
              setKycData(kycResponse.data.kyc);
            }
          } catch (error) {
            console.log('Error reloading KYC data:', error);
          }
        }}
      />

      {/* KYC Verified Modal */}
      {kycData && (
        <KYCVerifiedModal
          open={showKYCVerifiedModal}
          onOpenChange={setShowKYCVerifiedModal}
          kycData={{
            full_name: kycData.full_name || userInfo.name,
            status: kycData.status,
            verification_level: kycData.verification_level || 1,
            has_biometric: kycData.has_biometric || false
          }}
          onBiometricUpdate={async (images) => {
            // Update has_biometric flag in backend
            try {
              await axios.patch(`/api/kyc/update-biometric/${userInfo.user_id}`, {
                ...images
              });
              // Reload KYC data
              const kycResponse = await axios.get(`/api/kyc/status/${userInfo.user_id}`);
              if (kycResponse.data.success && kycResponse.data.kyc) {
                setKycData(kycResponse.data.kyc);
              }
            } catch (error) {
              throw new Error('Không gửi được ảnh xác thực. Vui lòng kiểm tra trạng thái KYC và thử lại.');
            }
          }}
        />
      )}
    </div>
  );
}

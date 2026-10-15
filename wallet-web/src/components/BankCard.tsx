import { useState } from 'react';
import { Eye, EyeOff, CreditCard } from 'lucide-react';
import { Button } from './ui/button';
import { Card, CardContent } from './ui/card';


interface BankCardProps {
  cardNumber?: string;
  holderName?: string;
  expiryDate?: string;
  balance?: number;
  bankName?: string;
  cardType?: 'visa' | 'mastercard';
  cardLevel?: 'platinum' | 'gold' | 'classic';
}

export default function BankCard({
  cardNumber = '•••• •••• ••• 9012',
  holderName = 'NGUYEN VAN A',
  expiryDate = '12/28',
  balance = 15750000,
  bankName = 'MB Bank',
  cardType = 'visa',
  cardLevel = 'platinum'
}: BankCardProps) {
  const [showBalance, setShowBalance] = useState(true);

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('vi-VN').format(amount) + ' VNĐ';
  };

  const getCardLogo = () => {
    if (cardType === 'visa') {
      return (
        <div className="text-right">
          <div className="text-white text-lg font-bold tracking-wide">VISA</div>
          <div className="text-white/80 text-xs capitalize">{cardLevel}</div>
        </div>
      );
    }
    return (
      <div className="text-right">
        <CreditCard className="w-8 h-8 text-white/80" />
        <div className="text-white/80 text-xs capitalize">{cardLevel}</div>
      </div>
    );
  };

  return (
    <div className="flex items-start space-x-4">
      {/* Card */}
      <Card className="flex-1 overflow-hidden border-0 shadow-xl">
        <CardContent className="p-0">
          {/* Card Visual */}
          <div className="bg-gradient-to-br from-slate-700 via-slate-800 to-slate-900 p-6 text-white relative h-[280px] flex flex-col">
            {/* Header Row - No overlap with logo */}
            <div className="flex justify-between items-start mb-6 pr-24">
              <div className="flex-1">
                <div className="text-sm text-white/70 mb-1">Số dư khả dụng</div>
                <div className="text-2xl font-bold">
                  {showBalance ? formatCurrency(balance) : '••••••••• VNĐ'}
                </div>
              </div>
              <div className="flex items-center space-x-2 flex-shrink-0">
                <button 
                  onClick={() => setShowBalance(!showBalance)}
                  className="p-1 hover:bg-white/10 rounded transition-colors"
                >
                  {showBalance ? <Eye className="w-4 h-4" /> : <EyeOff className="w-4 h-4" />}
                </button>
                <div className="text-sm bg-white/20 px-2 py-1 rounded text-white/90 whitespace-nowrap">
                  Hoạt động
                </div>
              </div>
            </div>

            {/* Card Logo - Fixed positioning, no overlap */}
            <div className="absolute top-6 right-6 z-20 w-20">
              {getCardLogo()}
            </div>

            {/* Card Number Section with Chip */}
            <div className="mb-8 relative flex items-center space-x-4">
              {/* Card chip - positioned inline with card number */}
              <div className="w-12 h-8 bg-gradient-to-br from-yellow-200 to-yellow-400 rounded-md opacity-90 flex-shrink-0"></div>
              
              <div className="flex-1">
                <div className="text-xl font-mono tracking-wider">
                  {showBalance ? cardNumber : '•••• •••• •••• ••••'}
                </div>
              </div>
              
              {/* Copy icon */}
              <div className="absolute right-0 top-0">
                <div className="w-6 h-6 border border-white/40 rounded flex items-center justify-center">
                  <div className="w-3 h-3 border border-white/60 rounded-sm"></div>
                </div>
              </div>
            </div>

            {/* Bottom Info Section */}
            <div className="flex justify-between items-end mb-6 mt-auto">
              <div>
                <div className="text-lg font-semibold">{holderName}</div>
              </div>
              <div className="text-right">
                <div className="text-sm text-white/70 mb-1">Hết hạn</div>
                <div className="text-lg font-semibold">{expiryDate}</div>
              </div>
            </div>

            {/* Action Buttons */}
            <div className="flex space-x-3">
              <Button 
                variant="outline" 
                size="sm" 
                className="flex-1 !bg-transparent !hover:bg-transparent border-white/30 text-white hover:border-white/50"
              >
                Khóa thẻ
              </Button>
              <Button 
                variant="outline" 
                size="sm" 
                className="flex-1 !bg-transparent !hover:bg-transparent border-white/30 text-white hover:border-white/50"
              >
                Đổi PIN
              </Button>
              <Button 
                variant="outline" 
                size="sm" 
                className="flex-1 !bg-transparent !hover:bg-transparent border-white/30 text-white hover:border-white/50"
              >
                Chi tiết
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Bank Name */}
      <div className="flex-shrink-0">
        <div className="bg-white rounded-lg p-3 shadow-md border border-gray-200 min-w-[120px] h-[80px] flex flex-col justify-center">
          <div className="text-sm text-gray-600 mb-1 text-center">Ngân hàng</div>
          <div className="font-semibold text-gray-900 text-center text-sm">{bankName}</div>
        </div>
      </div>
    </div>
  );
}
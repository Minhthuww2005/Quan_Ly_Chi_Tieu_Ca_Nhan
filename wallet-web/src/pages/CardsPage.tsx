import { useState, useEffect } from 'react';
import { ArrowLeft, Eye, EyeOff, Plus } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Card, CardContent } from '../components/ui/card';
import BankCard from '../components/BankCard';
import BottomNavigation from '../components/BottomNavigation';
import { cardAPI } from '../services/api';

interface BankCardData {
  card_id: number;
  card_number: string;
  card_holder_name: string;
  bank_name: string;
  card_type: 'visa' | 'mastercard';
  expiry_date: string;
  balance?: number;
}

export default function CardsPage() {
  const navigate = useNavigate();
  const [showBalance, setShowBalance] = useState(true);
  const [cards, setCards] = useState<BankCardData[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchCards = async () => {
      try {
        const userInfo = localStorage.getItem('userInfo');
        if (!userInfo) {
          navigate('/');
          return;
        }

        const user = JSON.parse(userInfo);
        const response = await cardAPI.getCards(user.user_id);
        
        if (response.success) {
          setCards(response.cards || []);
        }
      } catch (error) {
        console.error('Error fetching cards:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchCards();
  }, [navigate]);

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <div className="bg-gradient-to-r from-gray-800 to-black px-6 py-8 text-white">
        <div className="flex items-center justify-between">
          <div className="flex items-center space-x-4">
            <button 
              onClick={() => navigate('/wallet')}
              className="p-2 hover:bg-white/10 rounded-full transition-colors"
            >
              <ArrowLeft className="w-6 h-6" />
            </button>
            <div>
              <div className="text-xl font-bold">Quản lý thẻ</div>
              <div className="text-sm opacity-80">Thẻ ghi nợ & tín dụng</div>
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

      {/* Cards List */}
      <div className="px-4 py-6 space-y-6">
        {loading ? (
          <div className="space-y-4">
            {[1, 2, 3].map((i) => (
              <div key={i} className="h-48 bg-gray-200 rounded-2xl animate-pulse"></div>
            ))}
          </div>
        ) : cards.length === 0 ? (
          <Card>
            <CardContent className="p-8 text-center">
              <div className="text-gray-500 mb-2">Chưa có thẻ nào</div>
              <div className="text-sm text-gray-400">Đăng ký thẻ mới để bắt đầu sử dụng</div>
            </CardContent>
          </Card>
        ) : (
          cards.map((card) => (
            <BankCard
              key={card.card_id}
              cardNumber={card.card_number}
              holderName={card.card_holder_name}
              expiryDate={card.expiry_date}
              balance={card.balance || 0}
              bankName={card.bank_name}
              cardType={card.card_type}
              cardLevel="platinum"
            />
          ))
        )}

        {/* Add New Card */}
        <Card onClick={() => navigate('/card-management')} className="border-2 border-dashed border-gray-300 hover:border-gray-400 transition-colors cursor-pointer">
          <CardContent className="p-8 text-center">
            <Plus className="w-12 h-12 text-gray-400 mx-auto mb-4" />
            <div className="text-gray-600 font-medium mb-2">Đăng ký thẻ mới</div>
            <div className="text-sm text-gray-500">Thẻ ghi nợ, tín dụng hoặc thẻ tiết kiệm</div>
          </CardContent>
        </Card>
      </div>

      <BottomNavigation />
    </div>
  );
}

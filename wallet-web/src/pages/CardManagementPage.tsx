/**
 * Card Management Page - Quản lý thẻ
 * Empty state with Add Card functionality
 */

import { useState, useEffect } from 'react';
import { ArrowLeft, Eye, EyeOff, Plus } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import BottomNavigation from '../components/BottomNavigation';
import AddCardModal from '../components/AddCardModal';
import { Button } from '../components/ui/button';

// Card interface
export interface Card {
  id: string;
  last4Digits: string;
  brand: 'visa' | 'mastercard' | 'jcb' | 'amex';
  holderName: string;
  expiryDate: string;
  type: 'debit' | 'credit';
  status: 'active' | 'blocked' | 'expired';
  issuer: string;
}

/**
 * Luhn Algorithm - Validate card number
 * @param cardNumber - Card number string (digits only)
 * @returns boolean - true if valid
 */
export function validateCardLuhn(cardNumber: string): boolean {
  // Remove spaces and non-digits
  const digits = cardNumber.replace(/\D/g, '');
  
  if (digits.length < 13 || digits.length > 19) {
    return false;
  }

  let sum = 0;
  let isEven = false;

  // Loop from right to left
  for (let i = digits.length - 1; i >= 0; i--) {
    let digit = parseInt(digits[i], 10);

    if (isEven) {
      digit *= 2;
      if (digit > 9) {
        digit -= 9;
      }
    }

    sum += digit;
    isEven = !isEven;
  }

  return sum % 10 === 0;
}

/**
 * Get card brand from card number
 * @param cardNumber - First 6 digits of card
 */
export function getCardBrand(cardNumber: string): Card['brand'] {
  const first2 = cardNumber.substring(0, 2);
  const first4 = cardNumber.substring(0, 4);

  if (first2 >= '51' && first2 <= '55') return 'mastercard';
  if (first2 === '34' || first2 === '37') return 'amex';
  if (first4 >= '3528' && first4 <= '3589') return 'jcb';
  return 'visa'; // Default
}

export default function CardManagementPage() {
  const navigate = useNavigate();
  const [showCardNumbers, setShowCardNumbers] = useState(false);
  const [cards, setCards] = useState<Card[]>([]);
  const [showAddCardModal, setShowAddCardModal] = useState(false);
  const [cardError, setCardError] = useState('');
  const [busyCard, setBusyCard] = useState('');
  const [_loading, setLoading] = useState(true);

  // Load cards from API
  useEffect(() => {
    const loadCards = async () => {
      try {
        const userInfo = localStorage.getItem('userInfo');
        if (!userInfo) return;

        const user = JSON.parse(userInfo);
        const response = await fetch(`/api/cards/${user.user_id}`);
        const data = await response.json();

        if (data.success && data.cards) {
          // Transform API response to Card interface
          const transformedCards: Card[] = data.cards.map((card: any) => ({
            id: 'card_' + card.card_id,
            last4Digits: card.last4_digits,
            brand: (card.brand || 'visa').toLowerCase() as Card['brand'],
            holderName: card.holder_name,
            expiryDate: card.expiry_date,
            type: card.type,
            status: card.status,
            issuer: card.issuer
          }));
          setCards(transformedCards);
        }
      } catch (error) {
        console.error('Load cards error:', error);
      } finally {
        setLoading(false);
      }
    };

    loadCards();
  }, []);

  const handleAddNewCard = () => {
    setShowAddCardModal(true);
  };

  const handleCardAdded = (newCard: Card) => {
    setCards([...cards, newCard]);
  };

  const toggleVisibility = () => {
    setShowCardNumbers(!showCardNumbers);
  };
  async function changeCard(card: Card, remove = false) {
    setBusyCard(card.id); setCardError('');
    try {
      const response = await fetch(`/api/cards/${card.id.replace('card_', '')}`, { method: remove ? 'DELETE' : 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ status: card.status === 'active' ? 'blocked' : 'active' }) });
      const data = await response.json(); if (!response.ok) throw new Error(data.message);
      setCards(items => remove ? items.filter(item => item.id !== card.id) : items.map(item => item.id === card.id ? { ...item, status: data.card.status } : item));
    } catch (e) { setCardError(e instanceof Error ? e.message : 'Không cập nhật được thẻ'); } finally { setBusyCard(''); }
  }

  return (
    <div className="min-h-screen bg-gray-50 pb-20">
      {/* Header */}
      <div className="bg-slate-900 px-6 py-6 text-white sticky top-0 z-50 shadow-lg">
        <div className="flex items-center justify-between mb-2">
          <button 
            onClick={() => navigate(-1)}
            className="p-2 hover:bg-white/10 rounded-full transition-colors"
          >
            <ArrowLeft className="w-6 h-6" />
          </button>
          <button 
            onClick={toggleVisibility}
            className="p-2 hover:bg-white/10 rounded-full transition-colors"
          >
            {showCardNumbers ? (
              <Eye className="w-6 h-6" />
            ) : (
              <EyeOff className="w-6 h-6" />
            )}
          </button>
        </div>
        <div>
          <h1 className="text-2xl font-bold">Quản lý thẻ</h1>
          <p className="text-sm text-gray-300 mt-1">Thẻ ghi nợ & tín dụng</p>
        </div>
      </div>

      {/* Main Content */}
      <div className="px-4 py-6">
        {cards.length === 0 ? (
          /* Empty State */
          <div className="bg-white rounded-2xl shadow-sm p-6">
            {/* No cards message */}
            <div className="text-center mb-6">
              <div className="w-20 h-20 bg-gray-100 rounded-full flex items-center justify-center mx-auto mb-4">
                <svg 
                  className="w-10 h-10 text-gray-400"
                  fill="none" 
                  stroke="currentColor" 
                  viewBox="0 0 24 24"
                >
                  <path 
                    strokeLinecap="round" 
                    strokeLinejoin="round" 
                    strokeWidth={2} 
                    d="M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z" 
                  />
                </svg>
              </div>
              <h3 className="text-lg font-semibold text-gray-700 mb-2">
                Chưa có thẻ nào
              </h3>
              <p className="text-sm text-gray-500">
                Đăng ký thẻ mới để bắt đầu sử dụng
              </p>
            </div>

            {/* Add Card Button - Dashed Border Box */}
            <button
              onClick={handleAddNewCard}
              className="w-full border-2 border-dashed border-gray-300 rounded-2xl py-12 hover:border-blue-500 hover:bg-blue-50 transition-all duration-200 group"
            >
              <div className="flex flex-col items-center justify-center">
                <div className="w-16 h-16 bg-gray-100 rounded-full flex items-center justify-center mb-4 group-hover:bg-blue-100 transition-colors">
                  <Plus className="w-8 h-8 text-gray-400 group-hover:text-blue-600 transition-colors" />
                </div>
                <p className="text-base font-medium text-gray-600 group-hover:text-blue-600 transition-colors">
                  Đăng ký thẻ mới
                </p>
                <p className="text-sm text-gray-400 mt-1">
                  Thẻ ghi nợ, tín dụng hoặc thẻ tiết kiệm
                </p>
              </div>
            </button>
          </div>
        ) : (
          /* Card List - Will be implemented when cards exist */
          <div className="space-y-4">
            {cardError && <p role="alert" className="text-red-600">{cardError}</p>}
            {cards.map((card) => (
              <div 
                key={card.id} 
                className="bg-gradient-to-br from-slate-800 to-slate-900 rounded-2xl p-6 text-white shadow-lg"
              >
                <div className="flex justify-between items-start mb-6">
                  <div>
                    <p className="text-sm text-gray-300 mb-1">{card.issuer}</p>
                    <p className="text-xs text-gray-400">{card.type === 'debit' ? 'Thẻ ghi nợ' : 'Thẻ tín dụng'}</p>
                  </div>
                  <div className="text-right">
                    <p className="text-xl font-bold uppercase">{card.brand}</p>
                  </div>
                </div>

                <div className="mb-4">
                  <p className="text-2xl font-mono tracking-wider">
                    {showCardNumbers 
                      ? `•••• •••• •••• ${card.last4Digits}`
                      : '•••• •••• •••• ••••'
                    }
                  </p>
                </div>

                <div className="flex justify-between items-end">
                  <div>
                    <p className="text-xs text-gray-400 mb-1">Chủ thẻ</p>
                    <p className="text-sm font-medium">{card.holderName}</p>
                  </div>
                  <div className="text-right">
                    <p className="text-xs text-gray-400 mb-1">Hết hạn</p>
                    <p className="text-sm font-medium">{card.expiryDate}</p>
                  </div>
                </div>
                <div className="mt-4 flex gap-3"><Button variant="secondary" disabled={busyCard === card.id} onClick={() => changeCard(card)}>{card.status === 'active' ? 'Khóa thẻ' : 'Mở khóa thẻ'}</Button><Button variant="secondary" disabled={busyCard === card.id} onClick={() => changeCard(card, true)}>Gỡ thẻ</Button></div>
              </div>
            ))}

            {/* Add More Card Button */}
            <Button
              onClick={handleAddNewCard}
              variant="outline"
              className="w-full py-6 border-2 border-dashed hover:border-blue-500 hover:bg-blue-50"
            >
              <Plus className="w-5 h-5 mr-2" />
              Thêm thẻ mới
            </Button>
          </div>
        )}

        {/* Info Cards */}
        <div className="mt-6 space-y-3">
          <div className="bg-blue-50 border border-blue-200 rounded-xl p-4">
            <div className="flex items-start gap-3">
              <div className="w-10 h-10 bg-blue-100 rounded-full flex items-center justify-center flex-shrink-0">
                <svg className="w-5 h-5 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
              </div>
              <div className="flex-1">
                <p className="text-sm font-medium text-blue-900 mb-1">
                  Lợi ích khi đăng ký thẻ
                </p>
                <p className="text-xs text-blue-700">
                  Nhận hoàn tiền lên đến 5% cho mọi giao dịch, thanh toán trực tuyến an toàn, và quản lý chi tiêu dễ dàng.
                </p>
              </div>
            </div>
          </div>

          <div className="bg-green-50 border border-green-200 rounded-xl p-4">
            <div className="flex items-start gap-3">
              <div className="w-10 h-10 bg-green-100 rounded-full flex items-center justify-center flex-shrink-0">
                <svg className="w-5 h-5 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
                </svg>
              </div>
              <div className="flex-1">
                <p className="text-sm font-medium text-green-900 mb-1">
                  An toàn & bảo mật
                </p>
                <p className="text-xs text-green-700">
                  Mọi giao dịch đều được mã hóa và bảo vệ bởi công nghệ bảo mật tiên tiến nhất.
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Bottom Navigation */}
      <BottomNavigation />

      {/* Add Card Modal */}
      <AddCardModal 
        open={showAddCardModal} 
        onOpenChange={setShowAddCardModal} 
        onCardAdded={handleCardAdded}
        existingCards={cards}
      />
    </div>
  );
}

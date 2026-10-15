/**
 * Add Card Modal - Form đăng ký thẻ mới
 */

import { useState } from 'react';
import { X, CreditCard, AlertCircle, CheckCircle, Info, HelpCircle } from 'lucide-react';
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
import { validateCardLuhn, getCardBrand, type Card } from '../pages/CardManagementPage';
import { VIETNAM_BANKS, TEST_CARD_NUMBERS } from '../data/vietnamBanks';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from './ui/select';
import { Checkbox } from './ui/checkbox';

interface AddCardModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCardAdded: (card: Card) => void;
  existingCards?: Card[]; // Để kiểm tra thẻ trùng
}

export default function AddCardModal({ open, onOpenChange, onCardAdded, existingCards = [] }: AddCardModalProps) {
  const [step, setStep] = useState(1); // 1: Card Info, 2: Processing, 3: Success
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  
  const [formData, setFormData] = useState({
    cardNumber: '',
    holderName: '',
    expiryMonth: '',
    expiryYear: '',
    cvv: '',
    type: 'debit' as 'debit' | 'credit',
    issuer: '',
    isDefault: false // Thẻ mặc định
  });

  const [cardValidation, setCardValidation] = useState({
    isValid: false,
    brand: '' as Card['brand']
  });

  const [showTestCards, setShowTestCards] = useState(false);

  // Format card number with spaces
  const formatCardNumber = (value: string) => {
    const digits = value.replace(/\D/g, '');
    const groups = digits.match(/.{1,4}/g);
    return groups ? groups.join(' ') : digits;
  };

  // Validate card number on change
  const handleCardNumberChange = (value: string) => {
    const formatted = formatCardNumber(value);
    setFormData({ ...formData, cardNumber: formatted });

    const digits = value.replace(/\D/g, '');
    if (digits.length >= 13) {
      const isValid = validateCardLuhn(digits);
      const brand = getCardBrand(digits);
      setCardValidation({ isValid, brand });
    } else {
      setCardValidation({ isValid: false, brand: 'visa' });
    }
  };

  // Format expiry month (01-12)
  const handleMonthChange = (value: string) => {
    const digits = value.replace(/\D/g, '').slice(0, 2);
    if (parseInt(digits) > 12) return;
    setFormData({ ...formData, expiryMonth: digits });
  };

  // Format expiry year (YY)
  const handleYearChange = (value: string) => {
    const digits = value.replace(/\D/g, '').slice(0, 2);
    setFormData({ ...formData, expiryYear: digits });
  };

  // Format CVV
  const handleCVVChange = (value: string) => {
    const digits = value.replace(/\D/g, '').slice(0, 4);
    setFormData({ ...formData, cvv: digits });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    // Validation
    if (!cardValidation.isValid) {
      setError('Số thẻ không hợp lệ');
      return;
    }

    if (!formData.holderName.trim()) {
      setError('Vui lòng nhập tên chủ thẻ');
      return;
    }

    if (!formData.expiryMonth || !formData.expiryYear) {
      setError('Vui lòng nhập ngày hết hạn');
      return;
    }

    if (formData.cvv.length < 3) {
      setError('CVV không hợp lệ');
      return;
    }

    if (!formData.issuer.trim()) {
      setError('Vui lòng chọn ngân hàng');
      return;
    }

    // Kiểm tra thẻ đã tồn tại
    const digits = formData.cardNumber.replace(/\D/g, '');
    const last4 = digits.slice(-4);
    const cardExists = existingCards.some(card => card.last4Digits === last4);
    
    if (cardExists) {
      setError(`Thẻ kết thúc bằng ${last4} đã được đăng ký`);
      return;
    }

    setLoading(true);
    setStep(2);

    try {
      // Get user info from localStorage
      const userInfo = localStorage.getItem('userInfo');
      if (!userInfo) {
        setError('Vui lòng đăng nhập lại');
        setLoading(false);
        setStep(1);
        return;
      }

      const user = JSON.parse(userInfo);
      const digits = formData.cardNumber.replace(/\D/g, '');

      // Call API to add card
      const response = await fetch('/api/cards/add', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          user_id: user.user_id,
          card_number: digits,
          holder_name: formData.holderName,
          expiry_month: formData.expiryMonth,
          expiry_year: formData.expiryYear,
          brand: cardValidation.brand,
          type: formData.type,
          issuer: formData.issuer
        })
      });

      const data = await response.json();

      if (data.success) {
        const newCard: Card = {
          id: 'card_' + data.card.card_id,
          last4Digits: data.card.last4_digits,
          brand: data.card.brand.toLowerCase(),
          holderName: data.card.holder_name,
          expiryDate: data.card.expiry_date,
          type: data.card.type,
          status: data.card.status,
          issuer: data.card.issuer
        };

        onCardAdded(newCard);
        setStep(3);
        setLoading(false);

        // Close after 2 seconds
        setTimeout(() => {
          handleClose();
        }, 2000);
      } else {
        setError(data.message || 'Có lỗi xảy ra');
        setLoading(false);
        setStep(1);
      }
    } catch (error) {
      console.error('Add card error:', error);
      setError('Không thể kết nối tới server');
      setLoading(false);
      setStep(1);
    }
  };

  const handleClose = () => {
    setStep(1);
    setError('');
    setFormData({
      cardNumber: '',
      holderName: '',
      expiryMonth: '',
      expiryYear: '',
      cvv: '',
      type: 'debit',
      issuer: '',
      isDefault: false
    });
    setCardValidation({ isValid: false, brand: 'visa' });
    onOpenChange(false);
  };

  const renderStep = () => {
    // Step 1: Form
    if (step === 1) {
      return (
        <form onSubmit={handleSubmit} className="space-y-4">
          {error && (
            <div className="bg-red-50 border border-red-200 rounded-lg p-3 flex items-start gap-2">
              <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0 mt-0.5" />
              <p className="text-red-800 text-sm">{error}</p>
            </div>
          )}

          {/* Card Number */}
          <div className="space-y-2">
            <Label htmlFor="cardNumber">Số thẻ *</Label>
            <div className="relative">
              <Input
                id="cardNumber"
                type="text"
                placeholder="0000 0000 0000 0000"
                value={formData.cardNumber}
                onChange={(e) => handleCardNumberChange(e.target.value)}
                maxLength={19}
                className={`font-mono ${cardValidation.isValid ? 'border-green-500' : ''}`}
                required
              />
              {formData.cardNumber.length >= 13 && (
                <div className="absolute right-3 top-1/2 -translate-y-1/2">
                  {cardValidation.isValid ? (
                    <CheckCircle className="w-5 h-5 text-green-600" />
                  ) : (
                    <X className="w-5 h-5 text-red-600" />
                  )}
                </div>
              )}
            </div>
            {cardValidation.isValid && (
              <p className="text-xs text-green-600 flex items-center gap-1">
                <CheckCircle className="w-3 h-3" />
                {cardValidation.brand.toUpperCase()} - Số thẻ hợp lệ
              </p>
            )}
          </div>

          {/* Card Holder Name */}
          <div className="space-y-2">
            <Label htmlFor="holderName">Tên chủ thẻ *</Label>
            <Input
              id="holderName"
              type="text"
              placeholder="NGUYEN VAN A"
              value={formData.holderName}
              onChange={(e) => setFormData({ ...formData, holderName: e.target.value.toUpperCase() })}
              className="uppercase"
              required
            />
          </div>

          {/* Expiry Date & CVV */}
          <div className="grid grid-cols-3 gap-3">
            <div className="space-y-2">
              <Label htmlFor="expiryMonth">Tháng *</Label>
              <Input
                id="expiryMonth"
                type="text"
                placeholder="MM"
                value={formData.expiryMonth}
                onChange={(e) => handleMonthChange(e.target.value)}
                maxLength={2}
                className="text-center"
                required
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="expiryYear">Năm *</Label>
              <Input
                id="expiryYear"
                type="text"
                placeholder="YY"
                value={formData.expiryYear}
                onChange={(e) => handleYearChange(e.target.value)}
                maxLength={2}
                className="text-center"
                required
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="cvv">CVV *</Label>
              <Input
                id="cvv"
                type="password"
                placeholder="***"
                value={formData.cvv}
                onChange={(e) => handleCVVChange(e.target.value)}
                maxLength={4}
                className="text-center"
                required
              />
            </div>
          </div>

          {/* Card Type */}
          <div className="space-y-2">
            <Label>Loại thẻ *</Label>
            <div className="grid grid-cols-2 gap-3">
              <button
                type="button"
                onClick={() => setFormData({ ...formData, type: 'debit' })}
                className={`p-3 border-2 rounded-lg text-sm font-medium transition-all ${
                  formData.type === 'debit'
                    ? 'border-blue-500 bg-blue-50 text-blue-700'
                    : 'border-gray-200 hover:border-gray-300'
                }`}
              >
                💳 Thẻ ghi nợ
              </button>
              <button
                type="button"
                onClick={() => setFormData({ ...formData, type: 'credit' })}
                className={`p-3 border-2 rounded-lg text-sm font-medium transition-all ${
                  formData.type === 'credit'
                    ? 'border-blue-500 bg-blue-50 text-blue-700'
                    : 'border-gray-200 hover:border-gray-300'
                }`}
              >
                💎 Thẻ tín dụng
              </button>
            </div>
          </div>

          {/* Issuer Bank */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <Label htmlFor="issuer">Ngân hàng phát hành *</Label>
              <button
                type="button"
                onClick={() => setShowTestCards(!showTestCards)}
                className="text-xs text-blue-600 hover:text-blue-700 flex items-center gap-1"
              >
                <Info className="w-3 h-3" />
                Số thẻ test
              </button>
            </div>
            
            <Select
              value={formData.issuer}
              onValueChange={(value) => setFormData({ ...formData, issuer: value })}
            >
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Chọn ngân hàng..." />
              </SelectTrigger>
              <SelectContent>
                {VIETNAM_BANKS.map((bank) => (
                  <SelectItem key={bank.code} value={bank.shortName}>
                    <div className="flex items-center gap-2">
                      <span>{bank.logo}</span>
                      <span>{bank.shortName}</span>
                    </div>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            {/* Test Cards Helper */}
            {showTestCards && (
              <div className="bg-blue-50 border border-blue-200 rounded-lg p-3 text-xs space-y-2">
                <p className="font-semibold text-blue-900 flex items-center gap-1">
                  <HelpCircle className="w-4 h-4" />
                  Số thẻ test hợp lệ:
                </p>
                <div className="space-y-1 text-blue-800 font-mono">
                  <div className="flex justify-between">
                    <span>Visa:</span>
                    <button
                      type="button"
                      onClick={() => {
                        handleCardNumberChange(TEST_CARD_NUMBERS.visa);
                        setShowTestCards(false);
                      }}
                      className="hover:text-blue-600 underline"
                    >
                      {TEST_CARD_NUMBERS.visa}
                    </button>
                  </div>
                  <div className="flex justify-between">
                    <span>Mastercard:</span>
                    <button
                      type="button"
                      onClick={() => {
                        handleCardNumberChange(TEST_CARD_NUMBERS.mastercard);
                        setShowTestCards(false);
                      }}
                      className="hover:text-blue-600 underline"
                    >
                      {TEST_CARD_NUMBERS.mastercard}
                    </button>
                  </div>
                  <div className="flex justify-between">
                    <span>Amex:</span>
                    <button
                      type="button"
                      onClick={() => {
                        handleCardNumberChange(TEST_CARD_NUMBERS.amex);
                        setShowTestCards(false);
                      }}
                      className="hover:text-blue-600 underline"
                    >
                      {TEST_CARD_NUMBERS.amex}
                    </button>
                  </div>
                </div>
                <p className="text-blue-700 italic">
                  * Chỉ dùng cho môi trường test
                </p>
              </div>
            )}
          </div>

          {/* Set as Default */}
          <div className="flex items-center space-x-2 bg-gray-50 p-3 rounded-lg">
            <Checkbox
              id="isDefault"
              checked={formData.isDefault}
              onCheckedChange={(checked) => 
                setFormData({ ...formData, isDefault: checked as boolean })
              }
            />
            <label
              htmlFor="isDefault"
              className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70 cursor-pointer"
            >
              Đặt làm thẻ mặc định
            </label>
          </div>

          {/* Buttons */}
          <div className="flex gap-2 pt-4">
            <Button
              type="button"
              variant="outline"
              className="flex-1"
              onClick={handleClose}
            >
              Hủy
            </Button>
            <Button
              type="submit"
              className="flex-1 bg-blue-600 hover:bg-blue-700"
              disabled={!cardValidation.isValid || loading}
            >
              <CreditCard className="w-4 h-4 mr-2" />
              Đăng ký thẻ
            </Button>
          </div>
        </form>
      );
    }

    // Step 2: Processing
    if (step === 2) {
      return (
        <div className="py-8 text-center">
          <div className="w-16 h-16 border-4 border-blue-600 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
          <h3 className="font-semibold text-lg mb-2">Đang xử lý...</h3>
          <p className="text-sm text-gray-600">Vui lòng chờ trong giây lát</p>
        </div>
      );
    }

    // Step 3: Success
    if (step === 3) {
      return (
        <div className="py-8 text-center">
          <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-4">
            <CheckCircle className="w-10 h-10 text-green-600" />
          </div>
          <h3 className="font-semibold text-lg mb-2 text-green-900">Thành công!</h3>
          <p className="text-sm text-gray-600">Thẻ đã được đăng ký</p>
        </div>
      );
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-[500px] max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <CreditCard className="w-5 h-5 text-blue-600" />
            Đăng ký thẻ mới
          </DialogTitle>
          <DialogDescription>
            {step === 1 && 'Nhập thông tin thẻ của bạn'}
            {step === 2 && 'Đang xác thực thông tin...'}
            {step === 3 && 'Hoàn tất đăng ký'}
          </DialogDescription>
        </DialogHeader>

        {renderStep()}
      </DialogContent>
    </Dialog>
  );
}

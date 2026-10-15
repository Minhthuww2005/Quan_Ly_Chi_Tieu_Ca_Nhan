/**
 * Enhanced Transfer Modals - MoMo-like features
 */

import { useState } from 'react';
import { ArrowRight, CheckCircle, AlertCircle, Phone, FileText, Building2, Loader2 } from 'lucide-react';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from './ui/dialog';
import { Button } from './ui/button';
import { Input } from './ui/input';
import { Label } from './ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from './ui/select';
import { PHONE_PROVIDERS, BILL_TYPES } from '../services/transactionService';
import { VIETNAM_BANKS, type BankInfo } from '../data/vietnamBanks';
import { bankAccountAPI, type BankAccountData } from '../services/api';

interface ModalProps {
  isOpen: boolean;
  onClose: () => void;
  userBalance: number;
  onSuccess: () => void;
}

// 1. WALLET TRANSFER MODAL - Chuyển ví sang ví
export function WalletTransferModal({ isOpen, onClose, userBalance, onSuccess }: ModalProps) {
  const [step, setStep] = useState<'input' | 'confirm' | 'pin' | 'processing' | 'success' | 'error'>('input');
  const [phone, setPhone] = useState('');
  const [amount, setAmount] = useState('');
  const [message, setMessage] = useState('');
  const [pin, setPin] = useState('');
  const [error, setError] = useState('');

  const handleNext = () => {
    if (!phone || !amount) {
      setError('Vui lòng nhập đầy đủ thông tin');
      return;
    }
    if (parseFloat(amount) > userBalance) {
      setError('Số dư không đủ');
      return;
    }
    setError('');
    setStep('confirm');
  };

  const handleConfirm = () => {
    setStep('pin');
  };

  const handleSubmit = async () => {
    if (pin.length !== 6) {
      setError('Mã PIN phải có 6 số');
      return;
    }
    
    setStep('processing');
    
    try {
      const userInfo = localStorage.getItem('userInfo');
      if (!userInfo) throw new Error('No user info');
      
      const user = JSON.parse(userInfo);
      
      const response = await fetch('/api/transactions/transfer', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          from_user_id: user.user_id,
          to_phone: phone,
          amount: parseFloat(amount),
          message,
          pin
        })
      });
      
      const data = await response.json();
      
      if (data.success) {
        setStep('success');
        setTimeout(() => {
          onSuccess();
          onClose();
          resetForm();
        }, 2000);
      } else {
        setError(data.message);
        setStep('error');
      }
    } catch (err: any) {
      setError(err.message || 'Có lỗi xảy ra');
      setStep('error');
    }
  };

  const resetForm = () => {
    setStep('input');
    setPhone('');
    setAmount('');
    setMessage('');
    setPin('');
    setError('');
  };

  const handleClose = () => {
    resetForm();
    onClose();
  };

  return (
    <Dialog open={isOpen} onOpenChange={handleClose}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center space-x-2">
            <ArrowRight className="w-5 h-5 text-blue-600" />
            <span>Chuyển tiền</span>
          </DialogTitle>
        </DialogHeader>

        {step === 'input' && (
          <div className="space-y-4">
            <div>
              <Label>Số điện thoại người nhận *</Label>
              <Input
                type="tel"
                placeholder="0912345678"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                maxLength={10}
              />
            </div>

            <div>
              <Label>Số tiền *</Label>
              <Input
                type="number"
                placeholder="50,000"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
              />
              <div className="text-xs text-gray-500 mt-1">
                Số dư: {userBalance.toLocaleString('vi-VN')} VNĐ
              </div>
            </div>

            <div>
              <Label>Lời nhắn (tùy chọn)</Label>
              <Input
                placeholder="Chuyển tiền"
                value={message}
                onChange={(e) => setMessage(e.target.value)}
              />
            </div>

            {error && (
              <div className="text-sm text-red-600 bg-red-50 p-3 rounded-lg flex items-center space-x-2">
                <AlertCircle className="w-4 h-4" />
                <span>{error}</span>
              </div>
            )}

            <div className="flex space-x-3">
              <Button variant="outline" className="flex-1" onClick={handleClose}>
                Hủy
              </Button>
              <Button className="flex-1" onClick={handleNext}>
                Tiếp tục
              </Button>
            </div>
          </div>
        )}

        {step === 'confirm' && (
          <div className="space-y-4">
            <div className="bg-gray-50 rounded-lg p-4 space-y-3">
              <div className="flex justify-between">
                <span className="text-gray-600">Người nhận:</span>
                <span className="font-medium">{phone}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600">Số tiền:</span>
                <span className="font-semibold text-blue-600">
                  {parseFloat(amount).toLocaleString('vi-VN')} VNĐ
                </span>
              </div>
              {message && (
                <div className="flex justify-between">
                  <span className="text-gray-600">Lời nhắn:</span>
                  <span className="font-medium">{message}</span>
                </div>
              )}
            </div>

            <div className="flex space-x-3">
              <Button variant="outline" className="flex-1" onClick={() => setStep('input')}>
                Quay lại
              </Button>
              <Button className="flex-1" onClick={handleConfirm}>
                Xác nhận
              </Button>
            </div>
          </div>
        )}

        {step === 'pin' && (
          <div className="space-y-4">
            <div>
              <Label>Nhập mã PIN</Label>
              <Input
                type="password"
                placeholder="******"
                value={pin}
                onChange={(e) => setPin(e.target.value.replace(/\D/g, ''))}
                maxLength={6}
                className="text-center text-2xl tracking-widest"
              />
            </div>

            {error && (
              <div className="text-sm text-red-600 bg-red-50 p-3 rounded-lg flex items-center space-x-2">
                <AlertCircle className="w-4 h-4" />
                <span>{error}</span>
              </div>
            )}

            <div className="flex space-x-3">
              <Button variant="outline" className="flex-1" onClick={() => setStep('confirm')}>
                Quay lại
              </Button>
              <Button className="flex-1" onClick={handleSubmit}>
                Chuyển tiền
              </Button>
            </div>
          </div>
        )}

        {step === 'processing' && (
          <div className="py-8 text-center">
            <div className="w-16 h-16 border-4 border-blue-600 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
            <div className="text-lg font-medium">Đang xử lý...</div>
          </div>
        )}

        {step === 'success' && (
          <div className="py-8 text-center">
            <CheckCircle className="w-16 h-16 text-green-600 mx-auto mb-4" />
            <div className="text-xl font-semibold mb-2">Chuyển tiền thành công!</div>
            <div className="text-gray-600">Giao dịch đã được hoàn tất</div>
          </div>
        )}

        {step === 'error' && (
          <div className="space-y-4">
            <div className="py-8 text-center">
              <AlertCircle className="w-16 h-16 text-red-600 mx-auto mb-4" />
              <div className="text-xl font-semibold mb-2">Giao dịch thất bại</div>
              <div className="text-red-600">{error}</div>
            </div>
            <Button className="w-full" onClick={() => setStep('input')}>
              Thử lại
            </Button>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

// 2. BANK TRANSFER MODAL - Chuyển đến ngân hàng
export function BankTransferModal({ isOpen, onClose, userBalance, onSuccess }: ModalProps) {
  const [step, setStep] = useState<'input' | 'confirm' | 'pin' | 'processing' | 'success' | 'error'>('input');
  const [bankCode, setBankCode] = useState('');
  const [accountNumber, setAccountNumber] = useState('');
  const [accountName, setAccountName] = useState('');
  const [amount, setAmount] = useState('');
  const [message, setMessage] = useState('');
  const [pin, setPin] = useState('');
  const [error, setError] = useState('');
  const [verifying, setVerifying] = useState(false);
  const [verifiedAccount, setVerifiedAccount] = useState<BankAccountData | null>(null);

  const fee = 1100;
  const total = parseFloat(amount) + fee || 0;

  // Auto verify account when bank code and account number are entered
  const handleAccountNumberChange = async (value: string) => {
    setAccountNumber(value);
    setAccountName('');
    setVerifiedAccount(null);

    // Cần CẢ bank_code VÀ account_number >= 8 digits
    if (!bankCode || value.length < 8) {
      return;
    }

    try {
      setVerifying(true);
      setError('');
      
      console.log('[Bank Transfer] Verifying:', bankCode, value);

      // Gọi API với CẢ HAI tham số bắt buộc
      const response = await bankAccountAPI.verifyAccount({
        bank_code: bankCode,
        account_number: value
      });

      if (response.success && response.data) {
        setVerifiedAccount(response.data);
        setAccountName(response.data.account_name);
        setError('');
        console.log('[Bank Transfer] Verified:', response.data.account_name, '-', response.data.bank_name);
      }
    } catch (err: any) {
      console.error('Verification error:', err);
      setVerifiedAccount(null);
      setAccountName('');
      // Don't show error immediately, user might still be typing
    } finally {
      setVerifying(false);
    }
  };

  const handleNext = () => {
    if (!bankCode || !accountNumber || !amount) {
      setError('Vui lòng nhập đầy đủ thông tin');
      return;
    }
    if (!verifiedAccount) {
      setError('Không tìm thấy tài khoản này tại ngân hàng đã chọn. Vui lòng kiểm tra lại ngân hàng và số tài khoản.');
      return;
    }
    if (total > userBalance) {
      setError('Số dư không đủ (bao gồm phí 1,100đ)');
      return;
    }
    setError('');
    setStep('confirm');
  };

  const handleConfirm = () => {
    setStep('pin');
  };

  const handleSubmit = async () => {
    if (pin.length !== 6) {
      setError('Mã PIN phải có 6 số');
      return;
    }
    
    setStep('processing');
    
    try {
      const userInfo = localStorage.getItem('userInfo');
      if (!userInfo) throw new Error('No user info');
      
      const user = JSON.parse(userInfo);
      
      const response = await fetch('/api/transactions/bank-transfer', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          user_id: user.user_id,
          bank_code: bankCode,
          account_number: accountNumber,
          account_name: accountName,
          amount: parseFloat(amount),
          message,
          pin
        })
      });
      
      const data = await response.json();
      
      if (data.success) {
        setStep('success');
        setTimeout(() => {
          onSuccess();
          onClose();
          resetForm();
        }, 2000);
      } else {
        setError(data.message);
        setStep('error');
      }
    } catch (err: any) {
      setError(err.message || 'Có lỗi xảy ra');
      setStep('error');
    }
  };

  const resetForm = () => {
    setStep('input');
    setBankCode('');
    setAccountNumber('');
    setAccountName('');
    setAmount('');
    setMessage('');
    setPin('');
    setError('');
    setVerifying(false);
    setVerifiedAccount(null);
  };

  const handleClose = () => {
    resetForm();
    onClose();
  };

  return (
    <Dialog open={isOpen} onOpenChange={handleClose}>
      <DialogContent className="max-w-md max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center space-x-2">
            <Building2 className="w-5 h-5 text-purple-600" />
            <span>Chuyển đến ngân hàng</span>
          </DialogTitle>
        </DialogHeader>

        {step === 'input' && (
          <div className="space-y-4">
            <div>
              <Label>Ngân hàng *</Label>
              <Select value={bankCode} onValueChange={setBankCode}>
                <SelectTrigger>
                  <SelectValue placeholder="Chọn ngân hàng" />
                </SelectTrigger>
                <SelectContent>
                  {VIETNAM_BANKS.map((bank: BankInfo) => (
                    <SelectItem key={bank.code} value={bank.code}>
                      <span className="flex items-center space-x-2">
                        <span>{bank.logo}</span>
                        <span>{bank.shortName}</span>
                      </span>
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div>
              <Label>Số tài khoản *</Label>
              <div className="relative">
                <Input
                  type="text"
                  placeholder="1234567890"
                  value={accountNumber}
                  onChange={(e) => handleAccountNumberChange(e.target.value)}
                  maxLength={20}
                />
                {verifying && (
                  <div className="absolute right-3 top-1/2 -translate-y-1/2">
                    <Loader2 className="w-4 h-4 animate-spin text-blue-500" />
                  </div>
                )}
              </div>
              {verifiedAccount && (
                <div className="mt-1 text-xs text-green-600 flex items-center space-x-1">
                  <CheckCircle className="w-3 h-3" />
                  <span>✓ Tài khoản hợp lệ tại {verifiedAccount.bank_name}</span>
                </div>
              )}
              {accountNumber.length >= 8 && !verifying && !verifiedAccount && bankCode && (
                <div className="mt-1 text-xs text-red-600 flex items-center space-x-1">
                  <AlertCircle className="w-3 h-3" />
                  <span>Không tìm thấy tài khoản tại ngân hàng này</span>
                </div>
              )}
            </div>

            <div>
              <Label>Tên chủ tài khoản</Label>
              <Input
                placeholder={verifying ? 'Đang tra cứu...' : bankCode ? 'Tự động hiển thị sau khi nhập STK' : 'Vui lòng chọn ngân hàng trước'}
                value={accountName}
                disabled
                className={verifiedAccount ? 'bg-green-50 border-green-300 text-green-900 font-medium' : 'bg-gray-50'}
              />
              {verifiedAccount && (
                <div className="mt-1 text-xs text-gray-600">
                  <span>Ngân hàng: {verifiedAccount.bank_name}</span>
                  {verifiedAccount.branch && <span> • Chi nhánh: {verifiedAccount.branch}</span>}
                </div>
              )}
            </div>

            <div>
              <Label>Số tiền *</Label>
              <Input
                type="number"
                placeholder="100,000"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
              />
              <div className="text-xs text-gray-500 mt-1 space-y-1">
                <div>Số dư: {userBalance.toLocaleString('vi-VN')} VNĐ</div>
                <div className="text-orange-600">Phí chuyển khoản: 1,100 VNĐ</div>
              </div>
            </div>

            <div>
              <Label>Nội dung chuyển khoản (tùy chọn)</Label>
              <Input
                placeholder="Chuyen tien"
                value={message}
                onChange={(e) => setMessage(e.target.value)}
              />
            </div>

            {error && (
              <div className="text-sm text-red-600 bg-red-50 p-3 rounded-lg flex items-center space-x-2">
                <AlertCircle className="w-4 h-4" />
                <span>{error}</span>
              </div>
            )}

            <div className="flex space-x-3">
              <Button variant="outline" className="flex-1" onClick={handleClose}>
                Hủy
              </Button>
              <Button className="flex-1" onClick={handleNext}>
                Tiếp tục
              </Button>
            </div>
          </div>
        )}

        {step === 'confirm' && (
          <div className="space-y-4">
            <div className="bg-gray-50 rounded-lg p-4 space-y-3">
              <div className="flex justify-between">
                <span className="text-gray-600">Ngân hàng:</span>
                <span className="font-medium">{VIETNAM_BANKS.find((b: BankInfo) => b.code === bankCode)?.shortName}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600">Số TK:</span>
                <span className="font-medium">{accountNumber}</span>
              </div>
              {accountName && (
                <div className="flex justify-between">
                  <span className="text-gray-600">Chủ TK:</span>
                  <span className="font-medium">{accountName}</span>
                </div>
              )}
              <div className="border-t pt-3 space-y-2">
                <div className="flex justify-between">
                  <span className="text-gray-600">Số tiền:</span>
                  <span className="font-medium">{parseFloat(amount).toLocaleString('vi-VN')} VNĐ</span>
                </div>
                <div className="flex justify-between text-sm">
                  <span className="text-gray-600">Phí:</span>
                  <span className="font-medium">1,100 VNĐ</span>
                </div>
                <div className="flex justify-between font-semibold text-lg border-t pt-2">
                  <span>Tổng:</span>
                  <span className="text-purple-600">{total.toLocaleString('vi-VN')} VNĐ</span>
                </div>
              </div>
            </div>

            <div className="flex space-x-3">
              <Button variant="outline" className="flex-1" onClick={() => setStep('input')}>
                Quay lại
              </Button>
              <Button className="flex-1" onClick={handleConfirm}>
                Xác nhận
              </Button>
            </div>
          </div>
        )}

        {step === 'pin' && (
          <div className="space-y-4">
            <div>
              <Label>Nhập mã PIN</Label>
              <Input
                type="password"
                placeholder="******"
                value={pin}
                onChange={(e) => setPin(e.target.value.replace(/\D/g, ''))}
                maxLength={6}
                className="text-center text-2xl tracking-widest"
              />
            </div>

            {error && (
              <div className="text-sm text-red-600 bg-red-50 p-3 rounded-lg flex items-center space-x-2">
                <AlertCircle className="w-4 h-4" />
                <span>{error}</span>
              </div>
            )}

            <div className="flex space-x-3">
              <Button variant="outline" className="flex-1" onClick={() => setStep('confirm')}>
                Quay lại
              </Button>
              <Button className="flex-1" onClick={handleSubmit}>
                Chuyển khoản
              </Button>
            </div>
          </div>
        )}

        {step === 'processing' && (
          <div className="py-8 text-center">
            <div className="w-16 h-16 border-4 border-purple-600 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
            <div className="text-lg font-medium">Đang xử lý...</div>
          </div>
        )}

        {step === 'success' && (
          <div className="py-8 text-center">
            <CheckCircle className="w-16 h-16 text-green-600 mx-auto mb-4" />
            <div className="text-xl font-semibold mb-2">Chuyển khoản thành công!</div>
            <div className="text-gray-600">Giao dịch đang được xử lý bởi ngân hàng</div>
          </div>
        )}

        {step === 'error' && (
          <div className="space-y-4">
            <div className="py-8 text-center">
              <AlertCircle className="w-16 h-16 text-red-600 mx-auto mb-4" />
              <div className="text-xl font-semibold mb-2">Giao dịch thất bại</div>
              <div className="text-red-600">{error}</div>
            </div>
            <Button className="w-full" onClick={() => setStep('input')}>
              Thử lại
            </Button>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

// 3. PHONE TOP-UP MODAL - Nạp tiền điện thoại
export function PhoneTopupModal({ isOpen, onClose, userBalance, onSuccess }: ModalProps) {
  const [step, setStep] = useState<'input' | 'confirm' | 'pin' | 'processing' | 'success' | 'error'>('input');
  const [provider, setProvider] = useState('');
  const [phone, setPhone] = useState('');
  const [amount, setAmount] = useState('');
  const [pin, setPin] = useState('');
  const [error, setError] = useState('');

  const quickAmounts = [10000, 20000, 50000, 100000, 200000, 500000];

  const handleNext = () => {
    if (!provider || !phone || !amount) {
      setError('Vui lòng nhập đầy đủ thông tin');
      return;
    }
    if (parseFloat(amount) > userBalance) {
      setError('Số dư không đủ');
      return;
    }
    setError('');
    setStep('confirm');
  };

  const handleConfirm = () => {
    setStep('pin');
  };

  const handleSubmit = async () => {
    if (pin.length !== 6) {
      setError('Mã PIN phải có 6 số');
      return;
    }
    
    setStep('processing');
    
    try {
      const userInfo = localStorage.getItem('userInfo');
      if (!userInfo) throw new Error('No user info');
      
      const user = JSON.parse(userInfo);
      
      const response = await fetch('/api/transactions/phone-topup', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          user_id: user.user_id,
          phone_number: phone,
          provider,
          amount: parseFloat(amount),
          pin
        })
      });
      
      const data = await response.json();
      
      if (data.success) {
        setStep('success');
        setTimeout(() => {
          onSuccess();
          onClose();
          resetForm();
        }, 2000);
      } else {
        setError(data.message);
        setStep('error');
      }
    } catch (err: any) {
      setError(err.message || 'Có lỗi xảy ra');
      setStep('error');
    }
  };

  const resetForm = () => {
    setStep('input');
    setProvider('');
    setPhone('');
    setAmount('');
    setPin('');
    setError('');
  };

  const handleClose = () => {
    resetForm();
    onClose();
  };

  const selectedProvider = PHONE_PROVIDERS.find(p => p.code === provider);

  return (
    <Dialog open={isOpen} onOpenChange={handleClose}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center space-x-2">
            <Phone className="w-5 h-5 text-pink-600" />
            <span>Nạp tiền điện thoại</span>
          </DialogTitle>
        </DialogHeader>

        {step === 'input' && (
          <div className="space-y-4">
            <div>
              <Label>Nhà mạng *</Label>
              <div className="grid grid-cols-3 gap-2 mt-2">
                {PHONE_PROVIDERS.map((p) => (
                  <button
                    key={p.code}
                    onClick={() => setProvider(p.code)}
                    className={`p-3 rounded-lg border-2 transition-all ${
                      provider === p.code
                        ? `border-[${p.color}] bg-[${p.color}]/5`
                        : 'border-gray-200 hover:border-gray-300'
                    }`}
                  >
                    <div className="text-2xl mb-1">{p.logo}</div>
                    <div className="text-xs font-medium">{p.name}</div>
                  </button>
                ))}
              </div>
            </div>

            <div>
              <Label>Số điện thoại *</Label>
              <Input
                type="tel"
                placeholder="0912345678"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                maxLength={10}
              />
            </div>

            <div>
              <Label>Mệnh giá *</Label>
              <div className="grid grid-cols-3 gap-2 mt-2 mb-3">
                {quickAmounts.map((amt) => (
                  <button
                    key={amt}
                    onClick={() => setAmount(amt.toString())}
                    className={`py-2 px-3 rounded-lg border-2 text-sm font-medium transition-all ${
                      amount === amt.toString()
                        ? 'border-pink-600 bg-pink-50 text-pink-700'
                        : 'border-gray-200 hover:border-gray-300'
                    }`}
                  >
                    {(amt / 1000).toLocaleString()}k
                  </button>
                ))}
              </div>
              <Input
                type="number"
                placeholder="Hoặc nhập số tiền khác"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
              />
              <div className="text-xs text-gray-500 mt-1">
                Số dư: {userBalance.toLocaleString('vi-VN')} VNĐ
              </div>
            </div>

            {error && (
              <div className="text-sm text-red-600 bg-red-50 p-3 rounded-lg flex items-center space-x-2">
                <AlertCircle className="w-4 h-4" />
                <span>{error}</span>
              </div>
            )}

            <div className="flex space-x-3">
              <Button variant="outline" className="flex-1" onClick={handleClose}>
                Hủy
              </Button>
              <Button className="flex-1" onClick={handleNext}>
                Tiếp tục
              </Button>
            </div>
          </div>
        )}

        {step === 'confirm' && (
          <div className="space-y-4">
            <div className="bg-gray-50 rounded-lg p-4 space-y-3">
              <div className="flex justify-between items-center">
                <span className="text-gray-600">Nhà mạng:</span>
                <div className="flex items-center space-x-2">
                  <span className="text-xl">{selectedProvider?.logo}</span>
                  <span className="font-medium">{selectedProvider?.name}</span>
                </div>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600">Số điện thoại:</span>
                <span className="font-medium">{phone}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600">Mệnh giá:</span>
                <span className="font-semibold text-pink-600">
                  {parseFloat(amount).toLocaleString('vi-VN')} VNĐ
                </span>
              </div>
            </div>

            <div className="flex space-x-3">
              <Button variant="outline" className="flex-1" onClick={() => setStep('input')}>
                Quay lại
              </Button>
              <Button className="flex-1" onClick={handleConfirm}>
                Xác nhận
              </Button>
            </div>
          </div>
        )}

        {step === 'pin' && (
          <div className="space-y-4">
            <div>
              <Label>Nhập mã PIN</Label>
              <Input
                type="password"
                placeholder="******"
                value={pin}
                onChange={(e) => setPin(e.target.value.replace(/\D/g, ''))}
                maxLength={6}
                className="text-center text-2xl tracking-widest"
              />
            </div>

            {error && (
              <div className="text-sm text-red-600 bg-red-50 p-3 rounded-lg flex items-center space-x-2">
                <AlertCircle className="w-4 h-4" />
                <span>{error}</span>
              </div>
            )}

            <div className="flex space-x-3">
              <Button variant="outline" className="flex-1" onClick={() => setStep('confirm')}>
                Quay lại
              </Button>
              <Button className="flex-1" onClick={handleSubmit}>
                Nạp tiền
              </Button>
            </div>
          </div>
        )}

        {step === 'processing' && (
          <div className="py-8 text-center">
            <div className="w-16 h-16 border-4 border-pink-600 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
            <div className="text-lg font-medium">Đang nạp tiền...</div>
          </div>
        )}

        {step === 'success' && (
          <div className="py-8 text-center">
            <CheckCircle className="w-16 h-16 text-green-600 mx-auto mb-4" />
            <div className="text-xl font-semibold mb-2">Nạp tiền thành công!</div>
            <div className="text-gray-600">Tiền đã được nạp vào {phone}</div>
          </div>
        )}

        {step === 'error' && (
          <div className="space-y-4">
            <div className="py-8 text-center">
              <AlertCircle className="w-16 h-16 text-red-600 mx-auto mb-4" />
              <div className="text-xl font-semibold mb-2">Giao dịch thất bại</div>
              <div className="text-red-600">{error}</div>
            </div>
            <Button className="w-full" onClick={() => setStep('input')}>
              Thử lại
            </Button>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

// 4. BILL PAYMENT MODAL - Thanh toán hóa đơn
export function BillPaymentModal({ isOpen, onClose, userBalance, onSuccess }: ModalProps) {
  const [step, setStep] = useState<'input' | 'confirm' | 'pin' | 'processing' | 'success' | 'error'>('input');
  const [billType, setBillType] = useState('');
  const [billCode, setBillCode] = useState('');
  const [amount, setAmount] = useState('');
  const [pin, setPin] = useState('');
  const [error, setError] = useState('');

  const handleNext = () => {
    if (!billType || !billCode || !amount) {
      setError('Vui lòng nhập đầy đủ thông tin');
      return;
    }
    if (parseFloat(amount) > userBalance) {
      setError('Số dư không đủ');
      return;
    }
    setError('');
    setStep('confirm');
  };

  const handleConfirm = () => {
    setStep('pin');
  };

  const handleSubmit = async () => {
    if (pin.length !== 6) {
      setError('Mã PIN phải có 6 số');
      return;
    }
    
    setStep('processing');
    
    try {
      const userInfo = localStorage.getItem('userInfo');
      if (!userInfo) throw new Error('No user info');
      
      const user = JSON.parse(userInfo);
      
      const response = await fetch('/api/transactions/bill-payment', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          user_id: user.user_id,
          bill_type: billType,
          bill_code: billCode,
          amount: parseFloat(amount),
          pin
        })
      });
      
      const data = await response.json();
      
      if (data.success) {
        setStep('success');
        setTimeout(() => {
          onSuccess();
          onClose();
          resetForm();
        }, 2000);
      } else {
        setError(data.message);
        setStep('error');
      }
    } catch (err: any) {
      setError(err.message || 'Có lỗi xảy ra');
      setStep('error');
    }
  };

  const resetForm = () => {
    setStep('input');
    setBillType('');
    setBillCode('');
    setAmount('');
    setPin('');
    setError('');
  };

  const handleClose = () => {
    resetForm();
    onClose();
  };

  const selectedBillType = BILL_TYPES.find(b => b.code === billType);

  return (
    <Dialog open={isOpen} onOpenChange={handleClose}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center space-x-2">
            <FileText className="w-5 h-5 text-red-600" />
            <span>Thanh toán hóa đơn</span>
          </DialogTitle>
        </DialogHeader>

        {step === 'input' && (
          <div className="space-y-4">
            <div>
              <Label>Loại hóa đơn *</Label>
              <Select value={billType} onValueChange={setBillType}>
                <SelectTrigger>
                  <SelectValue placeholder="Chọn loại hóa đơn" />
                </SelectTrigger>
                <SelectContent>
                  {BILL_TYPES.map((bill) => (
                    <SelectItem key={bill.code} value={bill.code}>
                      <span className="flex items-center space-x-2">
                        <span>{bill.icon}</span>
                        <span>{bill.name}</span>
                        <span className="text-xs text-gray-500">({bill.provider})</span>
                      </span>
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div>
              <Label>Mã khách hàng / Mã hóa đơn *</Label>
              <Input
                placeholder="Nhập mã khách hàng"
                value={billCode}
                onChange={(e) => setBillCode(e.target.value)}
              />
            </div>

            <div>
              <Label>Số tiền *</Label>
              <Input
                type="number"
                placeholder="100,000"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
              />
              <div className="text-xs text-gray-500 mt-1">
                Số dư: {userBalance.toLocaleString('vi-VN')} VNĐ
              </div>
            </div>

            {error && (
              <div className="text-sm text-red-600 bg-red-50 p-3 rounded-lg flex items-center space-x-2">
                <AlertCircle className="w-4 h-4" />
                <span>{error}</span>
              </div>
            )}

            <div className="flex space-x-3">
              <Button variant="outline" className="flex-1" onClick={handleClose}>
                Hủy
              </Button>
              <Button className="flex-1" onClick={handleNext}>
                Tiếp tục
              </Button>
            </div>
          </div>
        )}

        {step === 'confirm' && (
          <div className="space-y-4">
            <div className="bg-gray-50 rounded-lg p-4 space-y-3">
              <div className="flex justify-between items-center">
                <span className="text-gray-600">Loại hóa đơn:</span>
                <div className="flex items-center space-x-2">
                  <span>{selectedBillType?.icon}</span>
                  <span className="font-medium">{selectedBillType?.name}</span>
                </div>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600">Nhà cung cấp:</span>
                <span className="font-medium">{selectedBillType?.provider}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600">Mã KH:</span>
                <span className="font-medium">{billCode}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600">Số tiền:</span>
                <span className="font-semibold text-red-600">
                  {parseFloat(amount).toLocaleString('vi-VN')} VNĐ
                </span>
              </div>
            </div>

            <div className="flex space-x-3">
              <Button variant="outline" className="flex-1" onClick={() => setStep('input')}>
                Quay lại
              </Button>
              <Button className="flex-1" onClick={handleConfirm}>
                Xác nhận
              </Button>
            </div>
          </div>
        )}

        {step === 'pin' && (
          <div className="space-y-4">
            <div>
              <Label>Nhập mã PIN</Label>
              <Input
                type="password"
                placeholder="******"
                value={pin}
                onChange={(e) => setPin(e.target.value.replace(/\D/g, ''))}
                maxLength={6}
                className="text-center text-2xl tracking-widest"
              />
            </div>

            {error && (
              <div className="text-sm text-red-600 bg-red-50 p-3 rounded-lg flex items-center space-x-2">
                <AlertCircle className="w-4 h-4" />
                <span>{error}</span>
              </div>
            )}

            <div className="flex space-x-3">
              <Button variant="outline" className="flex-1" onClick={() => setStep('confirm')}>
                Quay lại
              </Button>
              <Button className="flex-1" onClick={handleSubmit}>
                Thanh toán
              </Button>
            </div>
          </div>
        )}

        {step === 'processing' && (
          <div className="py-8 text-center">
            <div className="w-16 h-16 border-4 border-red-600 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
            <div className="text-lg font-medium">Đang xử lý...</div>
          </div>
        )}

        {step === 'success' && (
          <div className="py-8 text-center">
            <CheckCircle className="w-16 h-16 text-green-600 mx-auto mb-4" />
            <div className="text-xl font-semibold mb-2">Thanh toán thành công!</div>
            <div className="text-gray-600">Hóa đơn đã được thanh toán</div>
          </div>
        )}

        {step === 'error' && (
          <div className="space-y-4">
            <div className="py-8 text-center">
              <AlertCircle className="w-16 h-16 text-red-600 mx-auto mb-4" />
              <div className="text-xl font-semibold mb-2">Giao dịch thất bại</div>
              <div className="text-red-600">{error}</div>
            </div>
            <Button className="w-full" onClick={() => setStep('input')}>
              Thử lại
            </Button>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

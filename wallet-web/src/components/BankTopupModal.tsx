import { useState, useEffect } from 'react';
import { ArrowRight, Loader2, Check, AlertCircle } from 'lucide-react';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../components/ui/select';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '../components/ui/dialog';
import { Badge } from '../components/ui/badge';
import { formatCurrency } from '../data/bankingData';
import { bankAPI, supportedBankAPI, bankAccountAPI, type SupportedBankData } from '../services/api';
import { notifyWalletChanged } from '../services/walletEvents';

interface BankTopupModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSetPinRequired?: () => void;
}

interface LinkedBank {
  bank_id: number;
  bank_name: string;
  account_last4: string;
  is_default: boolean;
}

export default function BankTopupModal({ isOpen, onClose, onSetPinRequired }: BankTopupModalProps) {
  const [step, setStep] = useState(1);
  const [linkedBanks, setLinkedBanks] = useState<LinkedBank[]>([]);
  const [loading, setLoading] = useState(false);
  const [showPinWarning, setShowPinWarning] = useState(false);
  const [processing, setProcessing] = useState(false);
  const [showAddBankForm, setShowAddBankForm] = useState(false);
  const [linkingBank, setLinkingBank] = useState(false);
  const [lookingUpAccount, setLookingUpAccount] = useState(false);
  const [accountLookupCache, setAccountLookupCache] = useState<Record<string, string>>({});
  const [supportedBanks, setSupportedBanks] = useState<SupportedBankData[]>([]);
  const [loadingBanks, setLoadingBanks] = useState(false);
  const [error, setError] = useState('');
  const [lookupError, setLookupError] = useState('');
  const [linkError, setLinkError] = useState('');
  const [formData, setFormData] = useState({
    linkedBankId: '',
    amount: '',
    pinCode: '',
    description: 'Nạp tiền vào ví'
  });
  const [newBankData, setNewBankData] = useState({
    bankName: '',
    accountNumber: '',
    accountHolder: ''
  });

  useEffect(() => {
    if (isOpen) {
      checkPinStatus();
      fetchLinkedBanks();
      fetchSupportedBanks();
    }
  }, [isOpen]);

  const checkPinStatus = () => {
    const userInfo = localStorage.getItem('userInfo');
    if (!userInfo) return;
    
    const user = JSON.parse(userInfo);
    // Check if user has PIN set (you might need to add hasPin field to userInfo)
    const hasPin = user.hasPin !== false; // Assume true unless explicitly false
    
    if (!hasPin) {
      setShowPinWarning(true);
    }
  };

  const fetchSupportedBanks = async () => {
    try {
      setLoadingBanks(true);
      const response = await supportedBankAPI.getSupportedBanks();
      if (response.success) {
        setSupportedBanks(response.data);
      }
    } catch (error) {
      console.error('Error fetching supported banks:', error);
    } finally {
      setLoadingBanks(false);
    }
  };

  const fetchLinkedBanks = async () => {
    try {
      setLoading(true);
      const userInfo = localStorage.getItem('userInfo');
      if (!userInfo) return;

      const user = JSON.parse(userInfo);
      const response = await bankAPI.getLinkedBanks(user.user_id);

      if (response.success) {
        setLinkedBanks(response.data.banks || []);
        // Auto-select default bank
        const defaultBank = response.data.banks.find((b: LinkedBank) => b.is_default);
        if (defaultBank) {
          setFormData(prev => ({ ...prev, linkedBankId: defaultBank.bank_id.toString() }));
        }
      }
    } catch (error) {
      console.error('Error fetching linked banks:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleInputChange = (field: string, value: string) => {
    setFormData(prev => ({ ...prev, [field]: value }));
  };

  const handleNext = () => {
    // Validate before moving to next step
    if (step === 1) {
      if (!formData.linkedBankId) {
        alert('Vui lòng chọn ngân hàng');
        return;
      }
      const amount = parseFloat(formData.amount);
      if (isNaN(amount) || amount < 50000) {
        alert('Số tiền nạp tối thiểu là 50,000 VND');
        return;
      }
      if (amount > 50000000) {
        alert('Số tiền nạp tối đa là 50,000,000 VND mỗi lần');
        return;
      }
    }
    if (step < 2) setStep(step + 1);
  };

  const handleBack = () => {
    if (step > 1) setStep(step - 1);
  };

  const handleTopUp = async () => {
    try {
      setError('');
      
      const userInfo = localStorage.getItem('userInfo');
      if (!userInfo) {
        setError('Vui lòng đăng nhập');
        return;
      }

      const user = JSON.parse(userInfo);

      if (!formData.pinCode || formData.pinCode.length !== 6) {
        setError('Vui lòng nhập mã PIN 6 số');
        return;
      }

      setProcessing(true);

      const response = await bankAPI.topUp({
        user_id: user.user_id,
        linked_bank_id: parseInt(formData.linkedBankId),
        amount: parseFloat(formData.amount),
        pin_code: formData.pinCode,
        description: formData.description
      });

      if (response.success) {
        alert(`✅ Nạp tiền thành công!\n\nSố tiền: ${formatCurrency(parseFloat(formData.amount))}\nSố dư mới: ${formatCurrency(response.data.wallet_new_balance)}`);
        
        // Reset form
        setFormData({
          linkedBankId: '',
          amount: '',
          pinCode: '',
          description: 'Nạp tiền vào ví'
        });
        setStep(1);
        onClose();
        
        // Balance and history refresh in place; no page reload.
        notifyWalletChanged();
      }
    } catch (error: any) {
      console.error('Top-up error:', error);
      
      // Check if error is PIN_NOT_SET
      const errorMessage = error.message || 'Lỗi không xác định';
      if (errorMessage.includes('chưa thiết lập mã PIN') || errorMessage.includes('PIN')) {
        setShowPinWarning(true);
        return;
      }
      
      alert(`❌ Nạp tiền thất bại!\n\n${errorMessage}`);
    } finally {
      setProcessing(false);
    }
  };

  const handleAddNewBank = () => {
    setShowAddBankForm(true);
  };

  const handleCancelAddBank = () => {
    setShowAddBankForm(false);
    setNewBankData({ bankName: '', accountNumber: '', accountHolder: '' });
  };

  const handleNewBankInputChange = (field: string, value: string) => {
    setNewBankData(prev => ({ ...prev, [field]: value }));
  };

  // Tra cứu tên chủ tài khoản từ ngân hàng (database thật)
  const lookupAccountHolder = async () => {
    setLookupError('');
    
    if (!newBankData.bankName || !newBankData.accountNumber) {
      setLookupError('Vui lòng chọn ngân hàng VÀ nhập số tài khoản');
      return;
    }

    if (newBankData.accountNumber.length < 8 || newBankData.accountNumber.length > 20) {
      setLookupError('Số tài khoản phải từ 8-20 số');
      return;
    }

    try {
      setLookingUpAccount(true);
      
      // Get bank code from bank name
      const selectedBank = supportedBanks.find(b => b.bank_name === newBankData.bankName);
      if (!selectedBank) {
        setLookupError('Không tìm thấy mã ngân hàng');
        setLookingUpAccount(false);
        return;
      }

      // Check cache first (unique by bank_code + account_number)
      const cacheKey = `${selectedBank.bank_code}_${newBankData.accountNumber}`;
      if (accountLookupCache[cacheKey]) {
        setNewBankData(prev => ({ ...prev, accountHolder: accountLookupCache[cacheKey] }));
        setLookingUpAccount(false);
        return;
      }
      
      console.log('[Lookup Account] bank_code:', selectedBank.bank_code, 'account_number:', newBankData.accountNumber);

      // Call API với CẢ bank_code VÀ account_number
      const response = await bankAccountAPI.verifyAccount({
        bank_code: selectedBank.bank_code,
        account_number: newBankData.accountNumber
      });

      if (response.success && response.data) {
        const accountHolderName = response.data.account_name;
        
        // Save to cache
        setAccountLookupCache(prev => ({ ...prev, [cacheKey]: accountHolderName }));
        
        setNewBankData(prev => ({ ...prev, accountHolder: accountHolderName }));
      } else {
        throw new Error(response.message || 'Không tìm thấy tài khoản');
      }
      
    } catch (error: any) {
      console.error('Account lookup error:', error);
      setLookupError(`Không tìm thấy tài khoản tại ngân hàng ${newBankData.bankName}. Vui lòng kiểm tra lại số tài khoản.`);
    } finally {
      setLookingUpAccount(false);
    }
  };

  const handleLinkNewBank = async () => {
    try {
      setLinkError('');
      
      // Validate inputs
      if (!newBankData.bankName || !newBankData.accountNumber) {
        setLinkError('Vui lòng chọn ngân hàng VÀ nhập số tài khoản');
        return;
      }

      if (!newBankData.accountHolder) {
        setLinkError('Vui lòng nhấn "Tra cứu" để xác minh tài khoản từ ngân hàng');
        return;
      }

      if (newBankData.accountNumber.length < 8 || newBankData.accountNumber.length > 20) {
        setLinkError('Số tài khoản phải từ 8-20 số');
        return;
      }

      const userInfo = localStorage.getItem('userInfo');
      if (!userInfo) {
        setLinkError('Vui lòng đăng nhập');
        return;
      }

      const user = JSON.parse(userInfo);
      setLinkingBank(true);

      // Call API to link bank
      const response = await bankAPI.linkBank({
        user_id: user.user_id,
        bank_name: newBankData.bankName,
        account_number: newBankData.accountNumber,
        account_holder: newBankData.accountHolder.toUpperCase(),
        gateway_transaction_id: `GW_LINK_${Date.now()}`
      });

      if (response.success) {
        const bankData = response.data;
        alert(`✅ Liên kết ngân hàng thành công!\n\nNgân hàng: ${bankData?.bank_name || newBankData.bankName}\n4 số cuối: ${bankData?.account_last4 || 'N/A'}`);
        
        // Refresh linked banks
        await fetchLinkedBanks();
        
        // Close form
        handleCancelAddBank();
      } else {
        alert('❌ Liên kết thất bại: ' + (response.message || 'Lỗi không xác định'));
      }
    } catch (error: any) {
      console.error('Error linking bank:', error);
      alert('❌ Lỗi khi liên kết ngân hàng: ' + (error.message || 'Vui lòng thử lại'));
    } finally {
      setLinkingBank(false);
    }
  };

  const renderAddBankForm = () => (
    <div className="space-y-4">
      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 mb-4">
        <h3 className="font-semibold text-blue-900 mb-2">🏦 Liên kết ngân hàng mới</h3>
        <p className="text-sm text-blue-700">Nhập thông tin tài khoản ngân hàng của bạn</p>
      </div>

      <div>
        <Label htmlFor="bankName">Tên ngân hàng *</Label>
        <Select 
          value={newBankData.bankName} 
          onValueChange={(value) => handleNewBankInputChange('bankName', value)}
          disabled={loadingBanks}
        >
          <SelectTrigger>
            <SelectValue placeholder={loadingBanks ? 'Đang tải...' : 'Chọn ngân hàng'} />
          </SelectTrigger>
          <SelectContent>
            {supportedBanks.map((bank) => (
              <SelectItem key={bank.bank_code} value={bank.bank_name}>
                {bank.bank_name}
              </SelectItem>
            ))}
            {supportedBanks.length === 0 && !loadingBanks && (
              <SelectItem value="" disabled>
                Không có ngân hàng hỗ trợ
              </SelectItem>
            )}
          </SelectContent>
        </Select>
      </div>

      <div>
        <Label htmlFor="accountNumber">Số tài khoản *</Label>
        <div className="flex gap-2">
          <Input
            id="accountNumber"
            value={newBankData.accountNumber}
            onChange={(e) => handleNewBankInputChange('accountNumber', e.target.value.replace(/[^0-9]/g, ''))}
            placeholder="Nhập số tài khoản (8-20 số)"
            maxLength={20}
            className="flex-1"
          />
          <Button 
            type="button"
            onClick={lookupAccountHolder} 
            variant="outline"
            disabled={lookingUpAccount || !newBankData.bankName || newBankData.accountNumber.length < 8}
            className="whitespace-nowrap"
          >
            {lookingUpAccount ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin" />
              </>
            ) : (
              '🔍 Tra cứu'
            )}
          </Button>
        </div>
        <p className="text-xs text-gray-500 mt-1">Nhấn "Tra cứu" để tự động lấy tên chủ tài khoản</p>
        {lookupError && (
          <div className="flex items-center gap-2 p-2 bg-red-50 border border-red-200 rounded text-xs">
            <AlertCircle className="w-3 h-3 text-red-600 flex-shrink-0" />
            <p className="text-red-600">{lookupError}</p>
          </div>
        )}
      </div>

      <div>
        <Label htmlFor="accountHolder">Tên chủ thẻ *</Label>
        <Input
          id="accountHolder"
          value={newBankData.accountHolder}
          readOnly
          placeholder="Sẽ tự động hiển thị sau khi tra cứu"
          className={newBankData.accountHolder ? 'bg-green-50 border-green-300' : 'bg-gray-50'}
          style={{ textTransform: 'uppercase' }}
        />
        <p className="text-xs text-gray-500 mt-1">
          {newBankData.accountHolder ? (
            <span className="text-green-600 font-medium">✓ Đã tra cứu từ ngân hàng</span>
          ) : (
            <span>Nhấn nút "Tra cứu" ở trên để lấy tên chủ tài khoản</span>
          )}
        </p>
      </div>

      <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-3">
        <p className="text-xs text-yellow-800">
          <strong>Lưu ý:</strong> Tên chủ tài khoản được tra cứu trực tiếp từ database các tài khoản ngân hàng thật. 
          Vui lòng kiểm tra kỹ thông tin trước khi liên kết.
        </p>
      </div>

      {linkError && (
        <div className="flex items-center gap-2 p-3 bg-red-50 border border-red-200 rounded-lg">
          <AlertCircle className="w-4 h-4 text-red-600 flex-shrink-0" />
          <p className="text-sm text-red-600">{linkError}</p>
        </div>
      )}

      <div className="flex gap-2">
        <Button onClick={handleCancelAddBank} variant="outline" className="flex-1" disabled={linkingBank}>
          Hủy
        </Button>
        <Button onClick={handleLinkNewBank} className="flex-1" disabled={linkingBank}>
          {linkingBank ? (
            <>
              <Loader2 className="w-4 h-4 mr-2 animate-spin" />
              Đang liên kết...
            </>
          ) : (
            'Liên kết ngân hàng'
          )}
        </Button>
      </div>
    </div>
  );

  const renderStep1 = () => (
    <div className="space-y-4">
      <div>
        <Label htmlFor="linkedBank">Chọn ngân hàng đã liên kết</Label>
        {loading ? (
          <div className="flex items-center justify-center py-4">
            <Loader2 className="w-6 h-6 animate-spin text-blue-500" />
            <span className="ml-2 text-sm text-gray-500">Đang tải...</span>
          </div>
        ) : linkedBanks.length === 0 ? (
          <div className="text-center py-4">
            <p className="text-sm text-gray-500 mb-2">Bạn chưa liên kết ngân hàng nào</p>
            <Button onClick={handleAddNewBank} variant="outline" size="sm">
              + Thêm ngân hàng mới
            </Button>
          </div>
        ) : (
          <>
            <Select value={formData.linkedBankId} onValueChange={(value) => handleInputChange('linkedBankId', value)}>
              <SelectTrigger>
                <SelectValue placeholder="Chọn ngân hàng" />
              </SelectTrigger>
              <SelectContent>
                {linkedBanks.map(bank => (
                  <SelectItem key={bank.bank_id} value={bank.bank_id.toString()}>
                    <div className="flex items-center gap-2">
                      <span className="font-medium">{bank.bank_name}</span>
                      <span className="text-sm text-gray-500">****{bank.account_last4}</span>
                      {bank.is_default && <Badge variant="secondary">Mặc định</Badge>}
                    </div>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button onClick={handleAddNewBank} variant="link" size="sm" className="mt-2">
              + Thêm ngân hàng khác
            </Button>
          </>
        )}
      </div>

      <div>
        <Label htmlFor="amount">Số tiền nạp</Label>
        <Input
          id="amount"
          value={formData.amount}
          onChange={(e) => handleInputChange('amount', e.target.value)}
          placeholder="Nhập số tiền (50,000 - 50,000,000 VND)"
          type="number"
          min="50000"
          max="50000000"
        />
        <p className="text-xs text-gray-500 mt-1">
          Tối thiểu: 50,000 VND | Tối đa: 50,000,000 VND/lần | Giới hạn ngày: 200,000,000 VND
        </p>
      </div>

      <div className="grid grid-cols-3 gap-2">
        {['100000', '500000', '1000000', '2000000', '5000000', '10000000'].map(amount => (
          <Button
            key={amount}
            variant="outline"
            size="sm"
            onClick={() => handleInputChange('amount', amount)}
            className="text-xs"
          >
            {formatCurrency(parseInt(amount))}
          </Button>
        ))}
      </div>

      <div>
        <Label htmlFor="description">Nội dung (tùy chọn)</Label>
        <Input
          id="description"
          value={formData.description}
          onChange={(e) => handleInputChange('description', e.target.value)}
          placeholder="Nạp tiền vào ví"
        />
      </div>

      {error && (
        <div className="flex items-center gap-2 p-3 bg-red-50 border border-red-200 rounded-lg">
          <AlertCircle className="w-4 h-4 text-red-600 flex-shrink-0" />
          <p className="text-sm text-red-600">{error}</p>
        </div>
      )}
    </div>
  );

  const renderStep2 = () => {
    const selectedBank = linkedBanks.find(b => b.bank_id.toString() === formData.linkedBankId);
    
    return (
      <div className="space-y-4">
        <div className="bg-gray-50 p-4 rounded-lg space-y-3">
          <h3 className="font-semibold text-sm text-gray-700">Xác nhận thông tin nạp tiền</h3>
          
          <div className="flex justify-between text-sm">
            <span className="text-gray-600">Ngân hàng:</span>
            <span className="font-medium">
              {selectedBank?.bank_name} ****{selectedBank?.account_last4}
            </span>
          </div>

          <div className="flex justify-between text-sm">
            <span className="text-gray-600">Số tiền:</span>
            <span className="font-bold text-lg text-blue-600">
              {formatCurrency(parseFloat(formData.amount) || 0)}
            </span>
          </div>

          <div className="flex justify-between text-sm">
            <span className="text-gray-600">Phí giao dịch:</span>
            <span className="font-medium text-green-600">Miễn phí</span>
          </div>

          <div className="border-t pt-2 flex justify-between">
            <span className="font-semibold text-gray-700">Tổng cộng:</span>
            <span className="font-bold text-xl text-blue-600">
              {formatCurrency(parseFloat(formData.amount) || 0)}
            </span>
          </div>
        </div>

        <div>
          <Label htmlFor="pinCode">Mã PIN giao dịch</Label>
          <Input
            id="pinCode"
            value={formData.pinCode}
            onChange={(e) => {
              // Only allow 6 digits
              const value = e.target.value.replace(/\D/g, '').slice(0, 6);
              handleInputChange('pinCode', value);
            }}
            placeholder="Nhập mã PIN 6 số"
            type="password"
            maxLength={6}
            inputMode="numeric"
          />
          <p className="text-xs text-gray-500 mt-1">
            Nhập mã PIN 6 số để xác thực giao dịch
          </p>
        </div>

        <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-3">
          <p className="text-xs text-yellow-800">
            ⚠️ <strong>Lưu ý:</strong> Vui lòng kiểm tra kỹ thông tin trước khi xác nhận. 
            Giao dịch sẽ được xử lý ngay lập tức và không thể hoàn tác.
          </p>
        </div>

        {error && (
          <div className="flex items-center gap-2 p-3 bg-red-50 border border-red-200 rounded-lg">
            <AlertCircle className="w-4 h-4 text-red-600 flex-shrink-0" />
            <p className="text-sm text-red-600">{error}</p>
          </div>
        )}
      </div>
    );
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>
            {showAddBankForm ? 'Liên kết ngân hàng mới' : 'Nạp tiền từ ngân hàng'}
          </DialogTitle>
          <DialogDescription>
            {showAddBankForm && 'Nhập thông tin ngân hàng để liên kết'}
            {!showAddBankForm && step === 1 && 'Chọn ngân hàng và số tiền nạp'}
            {!showAddBankForm && step === 2 && 'Xác nhận và nhập mã PIN'}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {showAddBankForm ? (
            // Show Add Bank Form
            renderAddBankForm()
          ) : (
            <>
              {/* Progress Steps */}
              <div className="flex items-center justify-center space-x-2">
                {[1, 2].map((stepNumber) => (
                  <div key={stepNumber} className="flex items-center">
                    <div className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium ${
                      stepNumber <= step ? 'bg-blue-600 text-white' : 'bg-gray-200 text-gray-600'
                    }`}>
                      {stepNumber}
                    </div>
                    {stepNumber < 2 && (
                      <div className={`w-16 h-0.5 ${stepNumber < step ? 'bg-blue-600' : 'bg-gray-200'}`} />
                    )}
                  </div>
                ))}
              </div>

              {/* Step Content */}
              {step === 1 && renderStep1()}
              {step === 2 && renderStep2()}

              {/* Navigation Buttons */}
              <div className="flex space-x-3 pt-4">
                {step > 1 && (
                  <Button 
                    variant="outline" 
                    onClick={handleBack} 
                    className="flex-1"
                    disabled={processing}
                  >
                    Quay lại
                  </Button>
            )}
            {step < 2 ? (
              <Button 
                onClick={handleNext} 
                className="flex-1 bg-blue-600 hover:bg-blue-700"
                disabled={!formData.linkedBankId || !formData.amount || loading}
              >
                Tiếp tục
                <ArrowRight className="w-4 h-4 ml-2" />
              </Button>
            ) : (
              <Button 
                onClick={handleTopUp} 
                className="flex-1 bg-green-600 hover:bg-green-700"
                disabled={processing || formData.pinCode.length !== 6}
              >
                {processing ? (
                  <>
                    <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                    Đang xử lý...
                  </>
                ) : (
                  <>
                    <Check className="w-4 h-4 mr-2" />
                    Xác nhận nạp tiền
                  </>
                )}
              </Button>
            )}
          </div>
            </>
          )}
        </div>
      </DialogContent>

      {/* PIN Warning Dialog */}
      <Dialog open={showPinWarning} onOpenChange={setShowPinWarning}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle className="text-red-600">⚠️ Chưa thiết lập mã PIN</DialogTitle>
            <DialogDescription>
              Bạn cần thiết lập mã PIN để thực hiện giao dịch nạp tiền. 
              Mã PIN giúp bảo vệ tài khoản của bạn khỏi các giao dịch không mong muốn.
            </DialogDescription>
          </DialogHeader>
          <div className="flex flex-col space-y-3 pt-4">
            <Button
              onClick={() => {
                setShowPinWarning(false);
                onClose();
                onSetPinRequired?.();
              }}
              className="bg-blue-600 hover:bg-blue-700"
            >
              Thiết lập mã PIN ngay
            </Button>
            <Button
              variant="outline"
              onClick={() => {
                setShowPinWarning(false);
                onClose();
              }}
            >
              Để sau
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </Dialog>
  );
}

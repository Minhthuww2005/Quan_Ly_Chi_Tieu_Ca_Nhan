import { useState } from 'react';
import { X, ArrowRight } from 'lucide-react';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../components/ui/select';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '../components/ui/dialog';
import { Textarea } from '../components/ui/textarea';
import { formatCurrency } from '../data/bankingData';
import { transferAPI } from '../services/api';

interface TransferModalProps {
  isOpen: boolean;
  onClose: () => void;
  type: 'transfer' | 'topup' | null;
}

export default function TransferModal({ isOpen, onClose, type }: TransferModalProps) {
  const [step, setStep] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [formData, setFormData] = useState({
    recipientType: 'account',
    recipientAccount: '',
    recipientName: '',
    recipientBank: '',
    amount: '',
    content: '',
    phoneNumber: '',
    topupType: 'prepaid'
  });

  const banks = [
    'Vietcombank', 'BIDV', 'Agribank', 'Techcombank', 'MB Bank',
    'ACB', 'VPBank', 'Sacombank', 'HDBank', 'OCB'
  ];

  const handleInputChange = (field: string, value: string) => {
    setFormData(prev => ({ ...prev, [field]: value }));
  };

  const handleNext = () => {
    if (step < 3) setStep(step + 1);
  };

  const handleBack = () => {
    if (step > 1) setStep(step - 1);
  };

  const handleSubmit = async () => {
    setLoading(true);
    setError('');

    try {
      const userInfo = localStorage.getItem('userInfo');
      if (!userInfo) {
        alert('Vui lòng đăng nhập lại');
        return;
      }

      const user = JSON.parse(userInfo);
      
      // Chỉ xử lý chuyển tiền nội bộ qua số điện thoại
      if (type === 'transfer' && formData.phoneNumber) {
        // Yêu cầu nhập PIN
        const pinCode = prompt('Nhập mã PIN 6 số để xác nhận giao dịch:');
        if (!pinCode || !/^\d{6}$/.test(pinCode)) {
          alert('Mã PIN không hợp lệ! Vui lòng nhập 6 chữ số.');
          return;
        }

        const response = await transferAPI.execute({
          senderId: user.user_id,
          receiverPhone: formData.phoneNumber,
          amount: parseInt(formData.amount),
          pinCode: pinCode,
          description: formData.content || 'Chuyển tiền'
        });

        if (response.success) {
          alert(`✅ Chuyển tiền thành công!\n\nSố tiền: ${formatCurrency(parseInt(formData.amount))}\nNgười nhận: ${formData.phoneNumber}`);
          onClose();
          setStep(1);
          setFormData({
            recipientType: 'account',
            recipientAccount: '',
            recipientName: '',
            recipientBank: '',
            amount: '',
            content: '',
            phoneNumber: '',
            topupType: 'prepaid'
          });
          // Reload trang để cập nhật số dư
          window.location.reload();
        }
      } else {
        alert('Chức năng này đang được phát triển. Vui lòng chọn "Chuyển qua số điện thoại".');
      }
    } catch (err: any) {
      console.error('Transfer error:', err);
      setError(err.message || 'Chuyển tiền thất bại');
      alert('❌ ' + (err.message || 'Chuyển tiền thất bại. Vui lòng thử lại.'));
    } finally {
      setLoading(false);
    }
  };

  const renderTransferStep1 = () => (
    <div className="space-y-4">
      <div>
        <Label htmlFor="recipientType">Loại chuyển khoản</Label>
        <Select value={formData.recipientType} onValueChange={(value) => handleInputChange('recipientType', value)}>
          <SelectTrigger>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="account">Chuyển đến tài khoản ngân hàng</SelectItem>
            <SelectItem value="phone">Chuyển qua số điện thoại</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {formData.recipientType === 'account' ? (
        <>
          <div>
            <Label htmlFor="recipientBank">Ngân hàng</Label>
            <Select value={formData.recipientBank} onValueChange={(value) => handleInputChange('recipientBank', value)}>
              <SelectTrigger>
                <SelectValue placeholder="Chọn ngân hàng" />
              </SelectTrigger>
              <SelectContent>
                {banks.map(bank => (
                  <SelectItem key={bank} value={bank}>{bank}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label htmlFor="recipientAccount">Số tài khoản người nhận</Label>
            <Input
              id="recipientAccount"
              value={formData.recipientAccount}
              onChange={(e) => handleInputChange('recipientAccount', e.target.value)}
              placeholder="Nhập số tài khoản"
            />
          </div>
          <div>
            <Label htmlFor="recipientName">Tên người nhận</Label>
            <Input
              id="recipientName"
              value={formData.recipientName}
              onChange={(e) => handleInputChange('recipientName', e.target.value)}
              placeholder="Tên sẽ hiển thị sau khi nhập số tài khoản"
              disabled
            />
          </div>
        </>
      ) : (
        <div>
          <Label htmlFor="phoneNumber">Số điện thoại</Label>
          <Input
            id="phoneNumber"
            value={formData.phoneNumber}
            onChange={(e) => handleInputChange('phoneNumber', e.target.value)}
            placeholder="Nhập số điện thoại"
          />
        </div>
      )}
    </div>
  );

  const renderTopupStep1 = () => (
    <div className="space-y-4">
      <div>
        <Label htmlFor="phoneNumber">Số điện thoại</Label>
        <Input
          id="phoneNumber"
          value={formData.phoneNumber}
          onChange={(e) => handleInputChange('phoneNumber', e.target.value)}
          placeholder="Nhập số điện thoại cần nạp"
        />
      </div>
      <div>
        <Label htmlFor="topupType">Loại nạp tiền</Label>
        <Select value={formData.topupType} onValueChange={(value) => handleInputChange('topupType', value)}>
          <SelectTrigger>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="prepaid">Trả trước</SelectItem>
            <SelectItem value="postpaid">Trả sau</SelectItem>
          </SelectContent>
        </Select>
      </div>
    </div>
  );

  const renderStep2 = () => (
    <div className="space-y-4">
      <div>
        <Label htmlFor="amount">Số tiền</Label>
        <Input
          id="amount"
          value={formData.amount}
          onChange={(e) => handleInputChange('amount', e.target.value)}
          placeholder="Nhập số tiền"
          type="number"
        />
      </div>
      {type === 'topup' && (
        <div className="grid grid-cols-3 gap-2">
          {['50000', '100000', '200000', '300000', '500000', '1000000'].map(amount => (
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
      )}
      <div>
        <Label htmlFor="content">Nội dung {type === 'transfer' ? 'chuyển khoản' : 'nạp tiền'}</Label>
        <Textarea
          id="content"
          value={formData.content}
          onChange={(e) => handleInputChange('content', e.target.value)}
          placeholder={type === 'transfer' ? 'Nhập nội dung chuyển khoản' : 'Nội dung nạp tiền (tùy chọn)'}
          rows={3}
        />
      </div>
    </div>
  );

  const renderStep3 = () => (
    <div className="space-y-4">
      <div className="bg-gray-50 p-4 rounded-lg space-y-3">
        <h3 className="font-semibold text-gray-900">Xác nhận thông tin giao dịch</h3>
        
        {type === 'transfer' ? (
          <>
            <div className="flex justify-between">
              <span className="text-gray-600">Người nhận:</span>
              <span className="font-medium">{formData.recipientName || formData.phoneNumber}</span>
            </div>
            {formData.recipientAccount && (
              <div className="flex justify-between">
                <span className="text-gray-600">Số tài khoản:</span>
                <span className="font-medium">{formData.recipientAccount}</span>
              </div>
            )}
            {formData.recipientBank && (
              <div className="flex justify-between">
                <span className="text-gray-600">Ngân hàng:</span>
                <span className="font-medium">{formData.recipientBank}</span>
              </div>
            )}
          </>
        ) : (
          <>
            <div className="flex justify-between">
              <span className="text-gray-600">Số điện thoại:</span>
              <span className="font-medium">{formData.phoneNumber}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-600">Loại:</span>
              <span className="font-medium">{formData.topupType === 'prepaid' ? 'Trả trước' : 'Trả sau'}</span>
            </div>
          </>
        )}
        
        <div className="flex justify-between">
          <span className="text-gray-600">Số tiền:</span>
          <span className="font-medium text-red-600">{formatCurrency(parseInt(formData.amount) || 0)}</span>
        </div>
        
        {formData.content && (
          <div className="flex justify-between">
            <span className="text-gray-600">Nội dung:</span>
            <span className="font-medium">{formData.content}</span>
          </div>
        )}
        
        <div className="flex justify-between border-t pt-3">
          <span className="text-gray-600">Phí giao dịch:</span>
          <span className="font-medium">Miễn phí</span>
        </div>
      </div>
    </div>
  );

  const getTitle = () => {
    if (type === 'transfer') return 'Chuyển khoản';
    if (type === 'topup') return 'Nạp tiền điện thoại';
    return '';
  };

  const getStepTitle = () => {
    if (step === 1) return 'Thông tin người nhận';
    if (step === 2) return 'Số tiền và nội dung';
    if (step === 3) return 'Xác nhận giao dịch';
    return '';
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center justify-between">
            <span>{getTitle()}</span>
            <Button variant="ghost" size="sm" onClick={onClose}>
              <X className="w-4 h-4" />
            </Button>
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          {/* Progress Steps */}
          <div className="flex items-center justify-center space-x-2">
            {[1, 2, 3].map((stepNumber) => (
              <div key={stepNumber} className="flex items-center">
                <div className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium ${
                  stepNumber <= step ? 'bg-gray-900 text-white' : 'bg-gray-200 text-gray-600'
                }`}>
                  {stepNumber}
                </div>
                {stepNumber < 3 && (
                  <div className={`w-8 h-0.5 ${stepNumber < step ? 'bg-gray-900' : 'bg-gray-200'}`} />
                )}
              </div>
            ))}
          </div>

          <div className="text-center">
            <h3 className="font-medium text-gray-900">{getStepTitle()}</h3>
          </div>

          {/* Step Content */}
          {step === 1 && (type === 'transfer' ? renderTransferStep1() : renderTopupStep1())}
          {step === 2 && renderStep2()}
          {step === 3 && renderStep3()}

          {/* Error Message */}
          {error && (
            <div className="bg-red-50 border border-red-200 text-red-600 px-4 py-3 rounded-lg text-sm">
              {error}
            </div>
          )}

          {/* Navigation Buttons */}
          <div className="flex space-x-3 pt-4">
            {step > 1 && (
              <Button variant="outline" onClick={handleBack} className="flex-1" disabled={loading}>
                Quay lại
              </Button>
            )}
            {step < 3 ? (
              <Button onClick={handleNext} className="flex-1 bg-gray-900 hover:bg-gray-800" disabled={loading}>
                Tiếp tục
                <ArrowRight className="w-4 h-4 ml-2" />
              </Button>
            ) : (
              <Button onClick={handleSubmit} className="flex-1 bg-gray-900 hover:bg-gray-800" disabled={loading}>
                {loading ? (
                  <div className="flex items-center justify-center">
                    <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin mr-2"></div>
                    Đang xử lý...
                  </div>
                ) : (
                  'Xác nhận giao dịch'
                )}
              </Button>
            )}
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
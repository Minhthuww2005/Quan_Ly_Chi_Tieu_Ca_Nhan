/**
 * KYC Modal Component
 * Know Your Customer verification with encrypted data submission
 */

import { useState } from 'react';
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
import { ShieldCheck, Upload, AlertCircle } from 'lucide-react';

interface KYCModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  userId: number;
  onKYCSubmitted?: () => void;
}

export default function KYCModal({ 
  open, 
  onOpenChange, 
  userId,
  onKYCSubmitted
}: KYCModalProps) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);
  const [currentStep, setCurrentStep] = useState(1);

  const [formData, setFormData] = useState({
    full_name: '',
    date_of_birth: '',
    national_id: '',
    phone: '',
    email: '',
    address: '',
    city: '',
    district: '',
    document_type: 'cccd' as 'cccd' | 'cmnd' | 'passport' | 'driving_license',
    document_number: '',
    document_issue_date: '',
    document_issue_place: '',
  });

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      // Validation
      if (!formData.full_name || !formData.national_id || !formData.phone || !formData.email) {
        setError('Vui lòng điền đầy đủ thông tin bắt buộc');
        setLoading(false);
        return;
      }

      // Call API
      const response = await fetch('/api/kyc/submit', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          user_id: userId,
          ...formData
        })
      });

      const data = await response.json();

      if (data.success) {
        setSuccess(true);
        
        setTimeout(() => {
          setSuccess(false);
          onOpenChange(false);
          if (onKYCSubmitted) onKYCSubmitted();
        }, 3000);
      } else {
        setError(data.message || 'Đã xảy ra lỗi');
      }
    } catch (err: any) {
      setError(err.message || 'Không thể kết nối đến server');
    } finally {
      setLoading(false);
    }
  };

  const handleClose = () => {
    if (!loading) {
      setError('');
      setSuccess(false);
      setCurrentStep(1);
      onOpenChange(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-[600px] max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <ShieldCheck className="w-5 h-5 text-green-600" />
            Xác thực danh tính (KYC)
          </DialogTitle>
          <DialogDescription>
            Hoàn tất xác thực để mở khóa đầy đủ tính năng ví
          </DialogDescription>
        </DialogHeader>

        {success ? (
          <div className="space-y-4 py-4">
            <div className="bg-green-50 border border-green-200 rounded-lg p-6 text-center">
              <ShieldCheck className="w-16 h-16 text-green-600 mx-auto mb-4" />
              <p className="text-green-800 font-semibold text-lg mb-2">
                ✅ Gửi thông tin KYC thành công!
              </p>
              <p className="text-green-700 text-sm">
                Thông tin của bạn đã được mã hóa và lưu trữ an toàn. 
                <br />
                Chúng tôi sẽ xác minh trong vòng 24-48 giờ.
              </p>
            </div>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4 py-4">
            {error && (
              <div className="bg-red-50 border border-red-200 rounded-lg p-3 flex items-start gap-2">
                <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0 mt-0.5" />
                <p className="text-red-800 text-sm">{error}</p>
              </div>
            )}

            {/* Security Notice */}
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
              <p className="text-blue-900 text-sm font-medium mb-2">
                🔒 Thông tin được bảo mật AES-256
              </p>
              <p className="text-blue-700 text-xs">
                Tất cả thông tin nhạy cảm (CMND, ngày sinh, địa chỉ, SĐT) được mã hóa AES-256-GCM 
                trước khi lưu vào database. Tuân thủ PCI DSS & GDPR.
              </p>
            </div>

            {/* Step 1: Personal Information */}
            {currentStep === 1 && (
              <div className="space-y-4">
                <h3 className="font-semibold text-gray-900 flex items-center gap-2">
                  <span className="bg-blue-600 text-white w-6 h-6 rounded-full flex items-center justify-center text-sm">1</span>
                  Thông tin cá nhân
                </h3>

                <div className="grid grid-cols-2 gap-4">
                  <div className="col-span-2 space-y-2">
                    <Label htmlFor="full_name">Họ và tên đầy đủ *</Label>
                    <Input
                      id="full_name"
                      placeholder="Nguyễn Văn A"
                      value={formData.full_name}
                      onChange={(e) => setFormData({ ...formData, full_name: e.target.value })}
                      disabled={loading}
                      required
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="date_of_birth">Ngày sinh *</Label>
                    <Input
                      id="date_of_birth"
                      type="date"
                      value={formData.date_of_birth}
                      onChange={(e) => setFormData({ ...formData, date_of_birth: e.target.value })}
                      disabled={loading}
                      required
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="national_id">Số CMND/CCCD *</Label>
                    <Input
                      id="national_id"
                      placeholder="001234567890"
                      value={formData.national_id}
                      onChange={(e) => setFormData({ ...formData, national_id: e.target.value.replace(/\D/g, '') })}
                      disabled={loading}
                      required
                      maxLength={12}
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="phone">Số điện thoại *</Label>
                    <Input
                      id="phone"
                      placeholder="0901234567"
                      value={formData.phone}
                      onChange={(e) => setFormData({ ...formData, phone: e.target.value.replace(/\D/g, '') })}
                      disabled={loading}
                      required
                      maxLength={11}
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="email">Email *</Label>
                    <Input
                      id="email"
                      type="email"
                      placeholder="email@example.com"
                      value={formData.email}
                      onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                      disabled={loading}
                      required
                    />
                  </div>
                </div>

                <Button
                  type="button"
                  className="w-full bg-blue-600 hover:bg-blue-700"
                  onClick={() => {
                    if (formData.full_name && formData.national_id && formData.phone && formData.email && formData.date_of_birth) {
                      setCurrentStep(2);
                    } else {
                      setError('Vui lòng điền đầy đủ thông tin bắt buộc');
                    }
                  }}
                  disabled={loading}
                >
                  Tiếp tục →
                </Button>
              </div>
            )}

            {/* Step 2: Address & Documents */}
            {currentStep === 2 && (
              <div className="space-y-4">
                <h3 className="font-semibold text-gray-900 flex items-center gap-2">
                  <span className="bg-blue-600 text-white w-6 h-6 rounded-full flex items-center justify-center text-sm">2</span>
                  Địa chỉ & Giấy tờ
                </h3>

                <div className="space-y-4">
                  <div className="space-y-2">
                    <Label htmlFor="address">Địa chỉ cư trú</Label>
                    <Input
                      id="address"
                      placeholder="123 Đường ABC"
                      value={formData.address}
                      onChange={(e) => setFormData({ ...formData, address: e.target.value })}
                      disabled={loading}
                    />
                  </div>

                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-2">
                      <Label htmlFor="city">Tỉnh/Thành phố</Label>
                      <Input
                        id="city"
                        placeholder="Hà Nội"
                        value={formData.city}
                        onChange={(e) => setFormData({ ...formData, city: e.target.value })}
                        disabled={loading}
                      />
                    </div>

                    <div className="space-y-2">
                      <Label htmlFor="district">Quận/Huyện</Label>
                      <Input
                        id="district"
                        placeholder="Ba Đình"
                        value={formData.district}
                        onChange={(e) => setFormData({ ...formData, district: e.target.value })}
                        disabled={loading}
                      />
                    </div>
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="document_type">Loại giấy tờ</Label>
                    <select
                      id="document_type"
                      value={formData.document_type}
                      onChange={(e) => setFormData({ ...formData, document_type: e.target.value as any })}
                      disabled={loading}
                      className="w-full px-3 py-2 border border-gray-300 rounded-md"
                    >
                      <option value="cmnd">CMND (9 số)</option>
                      <option value="cccd">CCCD (12 số)</option>
                      <option value="passport">Hộ chiếu</option>
                      <option value="driving_license">Bằng lái xe</option>
                    </select>
                  </div>

                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-2">
                      <Label htmlFor="document_number">Số giấy tờ</Label>
                      <Input
                        id="document_number"
                        placeholder="001234567890"
                        value={formData.document_number}
                        onChange={(e) => setFormData({ ...formData, document_number: e.target.value })}
                        disabled={loading}
                      />
                    </div>

                    <div className="space-y-2">
                      <Label htmlFor="document_issue_date">Ngày cấp</Label>
                      <Input
                        id="document_issue_date"
                        type="date"
                        value={formData.document_issue_date}
                        onChange={(e) => setFormData({ ...formData, document_issue_date: e.target.value })}
                        disabled={loading}
                      />
                    </div>
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="document_issue_place">Nơi cấp</Label>
                    <Input
                      id="document_issue_place"
                      placeholder="Công an TP. Hà Nội"
                      value={formData.document_issue_place}
                      onChange={(e) => setFormData({ ...formData, document_issue_place: e.target.value })}
                      disabled={loading}
                    />
                  </div>
                </div>

                {/* Action Buttons */}
                <div className="flex gap-2 pt-2">
                  <Button
                    type="button"
                    variant="outline"
                    className="flex-1"
                    onClick={() => setCurrentStep(1)}
                    disabled={loading}
                  >
                    ← Quay lại
                  </Button>
                  <Button
                    type="submit"
                    className="flex-1 bg-green-600 hover:bg-green-700"
                    disabled={loading}
                  >
                    {loading ? (
                      <div className="flex items-center gap-2">
                        <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin"></div>
                        Đang gửi...
                      </div>
                    ) : (
                      <>
                        <Upload className="w-4 h-4 mr-2" />
                        Gửi xác thực
                      </>
                    )}
                  </Button>
                </div>
              </div>
            )}
          </form>
        )}
      </DialogContent>
    </Dialog>
  );
}

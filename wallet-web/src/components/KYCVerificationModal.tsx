/**
 * KYC Verification Modal with CCCD OCR Scanner
 * Complete ID Card verification with front, back and face capture
 */

import { useState, useRef } from 'react';
import { Button } from './ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from './ui/dialog';
import { 
  ShieldCheck, 
  Camera, 
  CheckCircle2, 
  AlertCircle, 
  Upload,
  User,
  CreditCard,
  ScanLine
} from 'lucide-react';

interface KYCVerificationModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  userId: number;
  onKYCSubmitted?: () => void;
}

interface CCCDData {
  id_number: string;
  full_name: string;
  date_of_birth: string;
  gender: string;
  nationality: string;
  place_of_origin: string;
  place_of_residence: string;
  expiry_date: string;
}

export default function KYCVerificationModal({ 
  open, 
  onOpenChange, 
  userId,
  onKYCSubmitted
}: KYCVerificationModalProps) {
  const [currentStep, setCurrentStep] = useState(1); // 1: Front, 2: Back, 3: Face, 4: Review
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);
  const [ocrProgress, setOcrProgress] = useState(0);

  // Images
  const [frontImage, setFrontImage] = useState<string | null>(null);
  const [backImage, setBackImage] = useState<string | null>(null);
  const [faceImage, setFaceImage] = useState<string | null>(null);

  // Extracted data
  const [cccdData, setCccdData] = useState<Partial<CCCDData>>({});
  const [backData, setBackData] = useState<any>({});

  // Refs
  const frontInputRef = useRef<HTMLInputElement>(null);
  const backInputRef = useRef<HTMLInputElement>(null);
  const faceInputRef = useRef<HTMLInputElement>(null);

  // OCR Processing
  const processOCR = async (imageFile: File, type: 'front' | 'back') => {
    setLoading(true);
    setError('');
    setOcrProgress(0);

    try {
      // Dynamic import to avoid build errors if package not installed
      const Tesseract = await import('tesseract.js');
      
      const result = await Tesseract.recognize(
        imageFile,
        'vie+eng', // Vietnamese + English
        {
          logger: (m: any) => {
            if (m.status === 'recognizing text') {
              setOcrProgress(Math.round(m.progress * 100));
            }
          }
        }
      );

      const text = result.data.text;
      console.log('OCR Result:', text);

      if (type === 'front') {
        const extracted = extractFrontData(text);
        if (!extracted.id_number || extracted.id_number.length !== 12) {
          setError('Không nhận diện được số CCCD 12 số. Vui lòng chụp lại rõ hơn.');
          return false;
        }
        setCccdData(extracted);
        return true;
      } else {
        const extracted = extractBackData(text);
        setBackData(extracted);
        return true;
      }
    } catch (err) {
      console.error('OCR Error:', err);
      setError('Lỗi khi quét ảnh. Vui lòng thử lại.');
      return false;
    } finally {
      setLoading(false);
      setOcrProgress(0);
    }
  };

  // Extract data from front side
  const extractFrontData = (text: string): Partial<CCCDData> => {
    const lines = text.split('\n').map(l => l.trim()).filter(l => l);
    const data: Partial<CCCDData> = {};

    // Find ID number (12 digits)
    const idMatch = text.match(/\b(\d{12})\b/);
    if (idMatch) {
      data.id_number = idMatch[1];
    }

    // Find full name (after "Họ và tên" or "Full name")
    const nameIndex = lines.findIndex(l => 
      l.includes('Họ và tên') || l.includes('Full name') || l.includes('Tên')
    );
    if (nameIndex >= 0 && nameIndex < lines.length - 1) {
      data.full_name = lines[nameIndex + 1].toUpperCase();
    }

    // Find date of birth (dd/mm/yyyy or dd-mm-yyyy)
    const dobMatch = text.match(/(\d{2})[\/\-](\d{2})[\/\-](\d{4})/);
    if (dobMatch) {
      data.date_of_birth = `${dobMatch[3]}-${dobMatch[2]}-${dobMatch[1]}`; // ISO format
    }

    // Find gender
    if (text.includes('Nam') || text.includes('Male')) {
      data.gender = 'Nam';
    } else if (text.includes('Nữ') || text.includes('Female')) {
      data.gender = 'Nữ';
    }

    // Find nationality
    if (text.includes('Việt Nam') || text.includes('Viet Nam')) {
      data.nationality = 'Việt Nam';
    }

    return data;
  };

  // Extract data from back side
  const extractBackData = (text: string): any => {
    const data: any = {};

    // Find issue date
    const issueDateMatch = text.match(/(\d{2})[\/\-](\d{2})[\/\-](\d{4})/);
    if (issueDateMatch) {
      data.issue_date = `${issueDateMatch[3]}-${issueDateMatch[2]}-${issueDateMatch[1]}`;
    }

    // Find expiry date (usually "Có giá trị đến")
    const expiryMatches = text.match(/(\d{2})[\/\-](\d{2})[\/\-](\d{4})/g);
    if (expiryMatches && expiryMatches.length > 1) {
      const exp = expiryMatches[1].match(/(\d{2})[\/\-](\d{2})[\/\-](\d{4})/);
      if (exp) {
        data.expiry_date = `${exp[3]}-${exp[2]}-${exp[1]}`;
      }
    }

    return data;
  };

  // Handle image upload
  const handleImageUpload = async (
    event: React.ChangeEvent<HTMLInputElement>, 
    type: 'front' | 'back' | 'face'
  ) => {
    const file = event.target.files?.[0];
    if (!file) return;

    // Validate file type
    if (!file.type.startsWith('image/')) {
      setError('Vui lòng chọn file ảnh');
      return;
    }

    // Validate file size (max 5MB)
    if (file.size > 5 * 1024 * 1024) {
      setError('Kích thước ảnh tối đa 5MB');
      return;
    }

    const reader = new FileReader();
    reader.onload = async (e) => {
      const imageData = e.target?.result as string;

      if (type === 'front') {
        setFrontImage(imageData);
        const success = await processOCR(file, 'front');
        if (success) {
          setTimeout(() => setCurrentStep(2), 500);
        }
      } else if (type === 'back') {
        setBackImage(imageData);
        const success = await processOCR(file, 'back');
        if (success) {
          setTimeout(() => setCurrentStep(3), 500);
        }
      } else if (type === 'face') {
        setFaceImage(imageData);
        setTimeout(() => setCurrentStep(4), 500);
      }
    };
    reader.readAsDataURL(file);
  };

  // Submit KYC
  const handleSubmit = async () => {
    setLoading(true);
    setError('');

    try {
      const response = await fetch('/api/kyc/submit-verification', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          user_id: userId,
          id_number: cccdData.id_number,
          full_name: cccdData.full_name,
          date_of_birth: cccdData.date_of_birth,
          gender: cccdData.gender,
          nationality: cccdData.nationality,
          front_image: frontImage,
          back_image: backImage,
          face_image: faceImage,
          issue_date: backData.issue_date,
          expiry_date: backData.expiry_date
        })
      });

      const data = await response.json();

      if (data.success) {
        setSuccess(true);
        setTimeout(() => {
          onOpenChange(false);
          if (onKYCSubmitted) onKYCSubmitted();
        }, 2000);
      } else {
        setError(data.message || 'Có lỗi xảy ra');
      }
    } catch (err) {
      setError('Không thể kết nối server');
    } finally {
      setLoading(false);
    }
  };

  const handleClose = () => {
    if (!loading) {
      setCurrentStep(1);
      setFrontImage(null);
      setBackImage(null);
      setFaceImage(null);
      setCccdData({});
      setBackData({});
      setError('');
      setSuccess(false);
      onOpenChange(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-[700px] max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <ShieldCheck className="w-5 h-5 text-green-600" />
            Xác thực CCCD - Bước {currentStep}/4
          </DialogTitle>
          <DialogDescription>
            {currentStep === 1 && 'Chụp mặt trước CCCD (có ảnh chân dung)'}
            {currentStep === 2 && 'Chụp mặt sau CCCD'}
            {currentStep === 3 && 'Chụp ảnh khuôn mặt của bạn'}
            {currentStep === 4 && 'Kiểm tra thông tin trước khi gửi'}
          </DialogDescription>
        </DialogHeader>

        {success ? (
          <div className="py-8 text-center">
            <CheckCircle2 className="w-20 h-20 text-green-600 mx-auto mb-4" />
            <h3 className="text-xl font-bold text-gray-900 mb-2">
              Xác thực thành công!
            </h3>
            <p className="text-gray-600">
              Thông tin đã được gửi. Chúng tôi sẽ xác minh trong 24-48h.
            </p>
          </div>
        ) : (
          <div className="space-y-6 py-4">
            {/* Progress Bar */}
            <div className="flex items-center justify-between mb-4">
              {[1, 2, 3, 4].map((step) => (
                <div key={step} className="flex items-center">
                  <div className={`w-10 h-10 rounded-full flex items-center justify-center font-semibold ${
                    step < currentStep ? 'bg-green-600 text-white' :
                    step === currentStep ? 'bg-blue-600 text-white' :
                    'bg-gray-200 text-gray-500'
                  }`}>
                    {step < currentStep ? <CheckCircle2 className="w-5 h-5" /> : step}
                  </div>
                  {step < 4 && (
                    <div className={`w-16 h-1 ${step < currentStep ? 'bg-green-600' : 'bg-gray-200'}`} />
                  )}
                </div>
              ))}
            </div>

            {error && (
              <div className="bg-red-50 border border-red-200 rounded-lg p-3 flex items-start gap-2">
                <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0 mt-0.5" />
                <p className="text-red-800 text-sm">{error}</p>
              </div>
            )}

            {loading && ocrProgress > 0 && (
              <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
                <div className="flex items-center gap-3 mb-2">
                  <ScanLine className="w-5 h-5 text-blue-600 animate-pulse" />
                  <span className="text-blue-900 font-medium">Đang quét và nhận diện...</span>
                </div>
                <div className="w-full bg-blue-200 rounded-full h-2">
                  <div 
                    className="bg-blue-600 h-2 rounded-full transition-all duration-300"
                    style={{ width: `${ocrProgress}%` }}
                  />
                </div>
                <p className="text-blue-700 text-sm mt-2">{ocrProgress}% hoàn thành</p>
              </div>
            )}

            {/* Step 1: Front Image */}
            {currentStep === 1 && (
              <div className="space-y-4">
                <div className="border-2 border-dashed border-gray-300 rounded-lg p-8 text-center">
                  {frontImage ? (
                    <div className="space-y-4">
                      <img src={frontImage} alt="CCCD mặt trước" className="max-h-64 mx-auto rounded" />
                      <Button 
                        variant="outline" 
                        onClick={() => frontInputRef.current?.click()}
                        disabled={loading}
                      >
                        <Camera className="w-4 h-4 mr-2" />
                        Chụp lại
                      </Button>
                    </div>
                  ) : (
                    <div className="space-y-4">
                      <CreditCard className="w-16 h-16 text-gray-400 mx-auto" />
                      <div>
                        <h4 className="font-semibold text-gray-900 mb-2">Chụp mặt trước CCCD</h4>
                        <p className="text-sm text-gray-600 mb-4">
                          Đặt CCCD trên nền trắng, đảm bảo đầy đủ 4 góc và chữ rõ ràng
                        </p>
                        <Button onClick={() => frontInputRef.current?.click()} disabled={loading}>
                          <Upload className="w-4 h-4 mr-2" />
                          Tải ảnh lên
                        </Button>
                      </div>
                    </div>
                  )}
                </div>
                <input
                  ref={frontInputRef}
                  type="file"
                  accept="image/*"
                  className="hidden"
                  onChange={(e) => handleImageUpload(e, 'front')}
                  disabled={loading}
                />

                {cccdData.id_number && (
                  <div className="bg-green-50 border border-green-200 rounded-lg p-4">
                    <h4 className="font-semibold text-green-900 mb-2 flex items-center gap-2">
                      <CheckCircle2 className="w-5 h-5" />
                      Thông tin đã quét được:
                    </h4>
                    <div className="space-y-1 text-sm">
                      <p><strong>Số CCCD:</strong> {cccdData.id_number}</p>
                      <p><strong>Họ tên:</strong> {cccdData.full_name}</p>
                      <p><strong>Ngày sinh:</strong> {cccdData.date_of_birth}</p>
                      <p><strong>Giới tính:</strong> {cccdData.gender}</p>
                      <p><strong>Quốc tịch:</strong> {cccdData.nationality}</p>
                    </div>
                  </div>
                )}
              </div>
            )}

            {/* Step 2: Back Image */}
            {currentStep === 2 && (
              <div className="space-y-4">
                <div className="border-2 border-dashed border-gray-300 rounded-lg p-8 text-center">
                  {backImage ? (
                    <div className="space-y-4">
                      <img src={backImage} alt="CCCD mặt sau" className="max-h-64 mx-auto rounded" />
                      <div className="flex gap-2 justify-center">
                        <Button 
                          variant="outline" 
                          onClick={() => backInputRef.current?.click()}
                          disabled={loading}
                        >
                          <Camera className="w-4 h-4 mr-2" />
                          Chụp lại
                        </Button>
                        <Button onClick={() => setCurrentStep(3)}>
                          Tiếp tục →
                        </Button>
                      </div>
                    </div>
                  ) : (
                    <div className="space-y-4">
                      <CreditCard className="w-16 h-16 text-gray-400 mx-auto rotate-180" />
                      <div>
                        <h4 className="font-semibold text-gray-900 mb-2">Chụp mặt sau CCCD</h4>
                        <p className="text-sm text-gray-600 mb-4">
                          Chụp rõ mã QR và thông tin nơi cấp, ngày hết hạn
                        </p>
                        <div className="flex gap-2 justify-center">
                          <Button variant="outline" onClick={() => setCurrentStep(1)}>
                            ← Quay lại
                          </Button>
                          <Button onClick={() => backInputRef.current?.click()} disabled={loading}>
                            <Upload className="w-4 h-4 mr-2" />
                            Tải ảnh lên
                          </Button>
                        </div>
                      </div>
                    </div>
                  )}
                </div>
                <input
                  ref={backInputRef}
                  type="file"
                  accept="image/*"
                  className="hidden"
                  onChange={(e) => handleImageUpload(e, 'back')}
                  disabled={loading}
                />
              </div>
            )}

            {/* Step 3: Face Image */}
            {currentStep === 3 && (
              <div className="space-y-4">
                <div className="border-2 border-dashed border-gray-300 rounded-lg p-8 text-center">
                  {faceImage ? (
                    <div className="space-y-4">
                      <img src={faceImage} alt="Ảnh khuôn mặt" className="max-h-64 mx-auto rounded-full" />
                      <div className="flex gap-2 justify-center">
                        <Button 
                          variant="outline" 
                          onClick={() => faceInputRef.current?.click()}
                          disabled={loading}
                        >
                          <Camera className="w-4 h-4 mr-2" />
                          Chụp lại
                        </Button>
                        <Button onClick={() => setCurrentStep(4)}>
                          Tiếp tục →
                        </Button>
                      </div>
                    </div>
                  ) : (
                    <div className="space-y-4">
                      <User className="w-16 h-16 text-gray-400 mx-auto" />
                      <div>
                        <h4 className="font-semibold text-gray-900 mb-2">Chụp ảnh khuôn mặt</h4>
                        <p className="text-sm text-gray-600 mb-4">
                          Nhìn thẳng vào camera, đảm bảo đủ ánh sáng và khuôn mặt rõ nét
                        </p>
                        <div className="flex gap-2 justify-center">
                          <Button variant="outline" onClick={() => setCurrentStep(2)}>
                            ← Quay lại
                          </Button>
                          <Button onClick={() => faceInputRef.current?.click()} disabled={loading}>
                            <Camera className="w-4 h-4 mr-2" />
                            Chụp ảnh
                          </Button>
                        </div>
                      </div>
                    </div>
                  )}
                </div>
                <input
                  ref={faceInputRef}
                  type="file"
                  accept="image/*"
                  className="hidden"
                  onChange={(e) => handleImageUpload(e, 'face')}
                  disabled={loading}
                />
              </div>
            )}

            {/* Step 4: Review */}
            {currentStep === 4 && (
              <div className="space-y-4">
                <div className="bg-gray-50 rounded-lg p-4 space-y-4">
                  <h4 className="font-semibold text-gray-900">Kiểm tra thông tin</h4>
                  
                  <div className="grid grid-cols-3 gap-4">
                    {frontImage && (
                      <div>
                        <p className="text-xs text-gray-600 mb-1">Mặt trước</p>
                        <img src={frontImage} alt="Front" className="w-full rounded border" />
                      </div>
                    )}
                    {backImage && (
                      <div>
                        <p className="text-xs text-gray-600 mb-1">Mặt sau</p>
                        <img src={backImage} alt="Back" className="w-full rounded border" />
                      </div>
                    )}
                    {faceImage && (
                      <div>
                        <p className="text-xs text-gray-600 mb-1">Khuôn mặt</p>
                        <img src={faceImage} alt="Face" className="w-full rounded border" />
                      </div>
                    )}
                  </div>

                  <div className="bg-white rounded p-3 text-sm space-y-2">
                    <p><strong>Số CCCD:</strong> {cccdData.id_number}</p>
                    <p><strong>Họ tên:</strong> {cccdData.full_name}</p>
                    <p><strong>Ngày sinh:</strong> {cccdData.date_of_birth}</p>
                    <p><strong>Giới tính:</strong> {cccdData.gender}</p>
                  </div>
                </div>

                <div className="flex gap-2">
                  <Button 
                    variant="outline" 
                    className="flex-1"
                    onClick={() => setCurrentStep(3)}
                    disabled={loading}
                  >
                    ← Chỉnh sửa
                  </Button>
                  <Button 
                    className="flex-1 bg-green-600 hover:bg-green-700"
                    onClick={handleSubmit}
                    disabled={loading}
                  >
                    {loading ? (
                      <>
                        <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin mr-2" />
                        Đang gửi...
                      </>
                    ) : (
                      <>
                        <CheckCircle2 className="w-4 h-4 mr-2" />
                        Xác nhận gửi
                      </>
                    )}
                  </Button>
                </div>
              </div>
            )}
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

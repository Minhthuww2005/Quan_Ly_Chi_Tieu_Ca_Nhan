/**
 * KYC Verified Modal - Advanced with Camera Scan & Face Recognition
 * Similar to MoMo KYC flow
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
import { Camera, CheckCircle, UserCheck, AlertCircle, Fingerprint, Scan } from 'lucide-react';

interface KYCVerifiedModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  kycData: {
    full_name: string;
    status: string;
    verification_level: number;
    has_biometric: boolean;
  };
  onBiometricUpdate?: (images: { front_image: string; back_image: string; face_image: string }) => Promise<void>;
}

export default function KYCVerifiedModal({ 
  open, 
  onOpenChange, 
  kycData,
  onBiometricUpdate
}: KYCVerifiedModalProps) {
  const [step, setStep] = useState<'info' | 'scan-front' | 'scan-back' | 'selfie' | 'processing' | 'complete'>('info');
  const [frontImage, setFrontImage] = useState<string | null>(null);
  const [backImage, setBackImage] = useState<string | null>(null);
  const [selfieImage, setSelfieImage] = useState<string | null>(null);
  const [cameraActive, setCameraActive] = useState(false);
  
  // Mark unused variables for future use (image upload to server)
  void backImage;
  void selfieImage;
  const [error, setError] = useState('');
  const videoRef = useRef<HTMLVideoElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const streamRef = useRef<MediaStream | null>(null);

  const startCamera = async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ 
        video: { facingMode: 'environment', width: 1280, height: 720 } 
      });
      if (videoRef.current) {
        videoRef.current.srcObject = stream;
        streamRef.current = stream;
        setCameraActive(true);
      }
    } catch (err: any) {
      setError('Không thể truy cập camera. Vui lòng cho phép quyền camera.');
      console.error('Camera error:', err);
    }
  };

  const stopCamera = () => {
    if (streamRef.current) {
      streamRef.current.getTracks().forEach(track => track.stop());
      streamRef.current = null;
    }
    setCameraActive(false);
  };

  const captureImage = () => {
    if (videoRef.current && canvasRef.current) {
      const video = videoRef.current;
      const canvas = canvasRef.current;
      canvas.width = video.videoWidth;
      canvas.height = video.videoHeight;
      const ctx = canvas.getContext('2d');
      if (ctx) {
        ctx.drawImage(video, 0, 0);
        const imageData = canvas.toDataURL('image/jpeg', 0.8);
        
        if (step === 'scan-front') {
          setFrontImage(imageData);
          stopCamera();
          setStep('scan-back');
        } else if (step === 'scan-back') {
          setBackImage(imageData);
          stopCamera();
          setStep('selfie');
        } else if (step === 'selfie') {
          setSelfieImage(imageData);
          stopCamera();
          setStep('processing');
          processKYCUpdate(imageData);
        }
      }
    }
  };

  const processKYCUpdate = async (faceImage: string) => {
    try {
      if (!frontImage || !backImage || !onBiometricUpdate) throw new Error('Cần đủ ảnh giấy tờ và ảnh khuôn mặt');
      await onBiometricUpdate({ front_image: frontImage, back_image: backImage, face_image: faceImage });
      setStep('complete');
    } catch (e) { setError(e instanceof Error ? e.message : 'Không gửi được ảnh'); setStep('info'); }
  };

  const handleClose = () => {
    stopCamera();
    setStep('info');
    setFrontImage(null);
    setBackImage(null);
    setSelfieImage(null);
    setError('');
    onOpenChange(false);
  };

  const renderContent = () => {
    // Step 1: Info - Show verification status
    if (step === 'info') {
      return (
        <div className="space-y-4 py-4">
          {/* Verification Status */}
          <div className="bg-green-50 border border-green-200 rounded-lg p-4">
            <div className="flex items-center gap-3">
              <CheckCircle className="w-10 h-10 text-green-600" />
              <div>
                <p className="font-semibold text-green-900">Đã xác minh danh tính</p>
                <p className="text-sm text-green-700">Cấp độ: Level {kycData.verification_level}</p>
              </div>
            </div>
          </div>

          {/* User Info */}
          <div className="bg-gray-50 rounded-lg p-4 space-y-2">
            <div className="flex justify-between">
              <span className="text-gray-600">Họ và tên:</span>
              <span className="font-semibold">{kycData.full_name}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-600">Trạng thái:</span>
              <span className="text-green-600 font-semibold">
                {kycData.status === 'verified' ? 'Đã xác minh' : 'Chờ xác minh'}
              </span>
            </div>
          </div>

          {/* Biometric Status */}
          {!kycData.has_biometric && (
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
              <div className="flex items-start gap-3">
                <Fingerprint className="w-6 h-6 text-blue-600 mt-1" />
                <div className="flex-1">
                  <p className="font-semibold text-blue-900 mb-2">Bổ sung ảnh xác thực để xét duyệt</p>
                  <p className="text-sm text-blue-700 mb-3">
                    Quét lại CCCD và khuôn mặt để tăng cường bảo mật và mở khóa các tính năng cao cấp
                  </p>
                  <Button
                    onClick={() => {
                      setStep('scan-front');
                      setTimeout(() => startCamera(), 300);
                    }}
                    className="w-full bg-blue-600 hover:bg-blue-700"
                  >
                    <Camera className="w-4 h-4 mr-2" />
                    Bắt đầu quét CCCD
                  </Button>
                </div>
              </div>
            </div>
          )}

          {/* Already has biometric */}
          {kycData.has_biometric && (
            <div className="bg-green-50 border border-green-200 rounded-lg p-4">
              <div className="flex items-center gap-3">
                <UserCheck className="w-6 h-6 text-green-600" />
                <div>
                  <p className="font-semibold text-green-900">Đã cập nhật sinh trắc học</p>
                  <p className="text-sm text-green-700">Tài khoản của bạn đã được bảo mật tối đa</p>
                </div>
              </div>
            </div>
          )}
        </div>
      );
    }

    // Step 2: Scan Front ID Card
    if (step === 'scan-front') {
      return (
        <div className="space-y-4 py-4">
          <div className="text-center mb-4">
            <Scan className="w-12 h-12 text-blue-600 mx-auto mb-2" />
            <h3 className="font-semibold text-lg">Quét mặt trước CCCD</h3>
            <p className="text-sm text-gray-600">Đặt CCCD trong khung hình</p>
          </div>

          {/* Camera View */}
          <div className="relative bg-black rounded-lg overflow-hidden" style={{ aspectRatio: '16/9' }}>
            <video
              ref={videoRef}
              autoPlay
              playsInline
              className="w-full h-full object-cover"
            />
            {/* Guide Overlay */}
            <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
              <div className="border-4 border-white rounded-lg opacity-70" style={{ width: '80%', height: '60%' }}></div>
            </div>
          </div>

          <canvas ref={canvasRef} className="hidden" />

          {error && (
            <div className="bg-red-50 border border-red-200 rounded-lg p-3 flex items-start gap-2">
              <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0 mt-0.5" />
              <p className="text-red-800 text-sm">{error}</p>
            </div>
          )}

          <div className="flex gap-2">
            <Button variant="outline" className="flex-1" onClick={handleClose}>
              Hủy
            </Button>
            <Button className="flex-1 bg-blue-600 hover:bg-blue-700" onClick={captureImage} disabled={!cameraActive}>
              <Camera className="w-4 h-4 mr-2" />
              Chụp
            </Button>
          </div>
        </div>
      );
    }

    // Step 3: Scan Back ID Card
    if (step === 'scan-back') {
      return (
        <div className="space-y-4 py-4">
          <div className="text-center mb-4">
            <Scan className="w-12 h-12 text-blue-600 mx-auto mb-2" />
            <h3 className="font-semibold text-lg">Quét mặt sau CCCD</h3>
            <p className="text-sm text-gray-600">Đặt mặt sau CCCD trong khung hình</p>
          </div>

          {/* Preview front image */}
          {frontImage && (
            <div className="flex items-center gap-2 bg-green-50 p-2 rounded">
              <CheckCircle className="w-5 h-5 text-green-600" />
              <span className="text-sm text-green-700">Đã chụp mặt trước</span>
            </div>
          )}

          {/* Camera View */}
          <div className="relative bg-black rounded-lg overflow-hidden" style={{ aspectRatio: '16/9' }}>
            <video
              ref={videoRef}
              autoPlay
              playsInline
              className="w-full h-full object-cover"
            />
            <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
              <div className="border-4 border-white rounded-lg opacity-70" style={{ width: '80%', height: '60%' }}></div>
            </div>
          </div>

          <canvas ref={canvasRef} className="hidden" />

          <div className="flex gap-2">
            <Button variant="outline" className="flex-1" onClick={() => { stopCamera(); setStep('scan-front'); }}>
              Quay lại
            </Button>
            <Button className="flex-1 bg-blue-600 hover:bg-blue-700" onClick={captureImage} disabled={!cameraActive}>
              <Camera className="w-4 h-4 mr-2" />
              Chụp
            </Button>
          </div>
        </div>
      );
    }

    // Step 4: Selfie
    if (step === 'selfie') {
      return (
        <div className="space-y-4 py-4">
          <div className="text-center mb-4">
            <UserCheck className="w-12 h-12 text-blue-600 mx-auto mb-2" />
            <h3 className="font-semibold text-lg">Chụp ảnh khuôn mặt</h3>
            <p className="text-sm text-gray-600">Nhìn thẳng vào camera và giữ yên</p>
          </div>

          {/* Progress */}
          <div className="flex justify-center gap-2 mb-4">
            <div className="w-8 h-8 bg-green-500 rounded-full flex items-center justify-center">
              <CheckCircle className="w-5 h-5 text-white" />
            </div>
            <div className="w-8 h-8 bg-green-500 rounded-full flex items-center justify-center">
              <CheckCircle className="w-5 h-5 text-white" />
            </div>
            <div className="w-8 h-8 bg-blue-500 rounded-full flex items-center justify-center text-white text-sm font-bold">
              3
            </div>
          </div>

          {/* Camera View - Face circle */}
          <div className="relative bg-black rounded-lg overflow-hidden" style={{ aspectRatio: '3/4' }}>
            <video
              ref={videoRef}
              autoPlay
              playsInline
              className="w-full h-full object-cover"
            />
            <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
              <div className="border-4 border-white rounded-full opacity-70" style={{ width: '60%', aspectRatio: '1/1' }}></div>
            </div>
          </div>

          <canvas ref={canvasRef} className="hidden" />

          <div className="flex gap-2">
            <Button variant="outline" className="flex-1" onClick={() => { stopCamera(); setStep('scan-back'); }}>
              Quay lại
            </Button>
            <Button className="flex-1 bg-blue-600 hover:bg-blue-700" onClick={captureImage} disabled={!cameraActive}>
              <Camera className="w-4 h-4 mr-2" />
              Chụp
            </Button>
          </div>
        </div>
      );
    }

    // Step 5: Processing
    if (step === 'processing') {
      return (
        <div className="space-y-4 py-8 text-center">
          <div className="w-16 h-16 border-4 border-blue-600 border-t-transparent rounded-full animate-spin mx-auto"></div>
          <h3 className="font-semibold text-lg">Đang xử lý...</h3>
          <p className="text-sm text-gray-600">Vui lòng chờ trong giây lát</p>
          <div className="space-y-2 text-left max-w-sm mx-auto">
            <div className="flex items-center gap-2">
              <div className="w-2 h-2 bg-blue-600 rounded-full animate-pulse"></div>
              <span className="text-sm text-gray-600">Xác thực thông tin CCCD...</span>
            </div>
            <div className="flex items-center gap-2">
              <div className="w-2 h-2 bg-blue-600 rounded-full animate-pulse"></div>
              <span className="text-sm text-gray-600">Nhận diện khuôn mặt...</span>
            </div>
            <div className="flex items-center gap-2">
              <div className="w-2 h-2 bg-blue-600 rounded-full animate-pulse"></div>
              <span className="text-sm text-gray-600">Đang gửi ảnh xác thực...</span>
            </div>
          </div>
        </div>
      );
    }

    // Step 6: Complete
    if (step === 'complete') {
      return (
        <div className="space-y-4 py-8 text-center">
          <CheckCircle className="w-20 h-20 text-green-600 mx-auto" />
          <h3 className="font-semibold text-xl text-green-900">Hoàn tất!</h3>
          <p className="text-gray-600">Ảnh xác thực đã được gửi; đang chờ quản trị viên duyệt</p>
          <div className="bg-green-50 border border-green-200 rounded-lg p-4 max-w-sm mx-auto">
            <p className="text-sm text-green-800">
              Ảnh được dùng để xét duyệt thủ công, chưa xác minh sinh trắc học tự động.
            </p>
          </div>
          <Button className="w-full bg-green-600 hover:bg-green-700" onClick={handleClose}>
            Đóng
          </Button>
        </div>
      );
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-[500px] max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <UserCheck className="w-5 h-5 text-blue-600" />
            Xác thực danh tính
          </DialogTitle>
          <DialogDescription>
            {step === 'info' && 'Thông tin xác minh của bạn'}
            {step === 'scan-front' && 'Bước 1/3: Quét mặt trước CCCD'}
            {step === 'scan-back' && 'Bước 2/3: Quét mặt sau CCCD'}
            {step === 'selfie' && 'Bước 3/3: Chụp ảnh khuôn mặt'}
            {step === 'processing' && 'Đang xử lý thông tin'}
            {step === 'complete' && 'Cập nhật thành công'}
          </DialogDescription>
        </DialogHeader>

        {renderContent()}
      </DialogContent>
    </Dialog>
  );
}

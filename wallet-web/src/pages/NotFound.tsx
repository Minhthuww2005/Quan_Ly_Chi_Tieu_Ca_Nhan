import { useNavigate } from 'react-router-dom';
import { Home, ArrowLeft, Wallet } from 'lucide-react';
import { Button } from '.././components/ui/button';
import { Card, CardContent } from '.././components/ui/card';

export default function NotFoundPage() {
  const navigate = useNavigate();

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-600 via-purple-600 to-pink-600 flex items-center justify-center p-4">
      <Card className="w-full max-w-md">
        <CardContent className="p-8 text-center">
          {/* Logo */}
          <div className="mb-6 flex justify-center">
            <div className="w-20 h-20 bg-gradient-to-br from-blue-600 to-purple-600 rounded-2xl flex items-center justify-center shadow-lg">
              <Wallet className="w-10 h-10 text-white" />
            </div>
          </div>

          {/* 404 Text */}
          <h1 className="text-8xl font-bold bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent mb-4">
            404
          </h1>
          
          {/* Message */}
          <h2 className="text-2xl font-bold text-gray-900 mb-2">
            Không tìm thấy trang
          </h2>
          <p className="text-gray-600 mb-8">
            Rất tiếc, trang bạn đang tìm kiếm không tồn tại hoặc đã bị di chuyển.
          </p>

          {/* Actions */}
          <div className="space-y-3">
            <Button 
              onClick={() => navigate('/')} 
              className="w-full bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 text-white"
            >
              <Home className="w-4 h-4 mr-2" />
              Về trang chủ
            </Button>
            <Button 
              onClick={() => navigate(-1)} 
              variant="outline"
              className="w-full"
            >
              <ArrowLeft className="w-4 h-4 mr-2" />
              Quay lại
            </Button>
          </div>

          {/* Footer */}
          <div className="mt-8 text-sm text-gray-500">
            HTD PAY - Ví điện tử thông minh
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

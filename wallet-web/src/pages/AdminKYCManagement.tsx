/**
 * Admin KYC Management - Verify user identity documents
 */

import { useState, useEffect } from 'react';
import { 
  ShieldCheck, 
  Search, 
  Filter, 
  Eye,
  Check,
  X,
  Clock,
  Image,
  User,
  FileText,
  AlertCircle,
  Download,
  ZoomIn
} from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { Badge } from '../components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../components/ui/select';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '../components/ui/dialog';
import { Label } from '../components/ui/label';
import { Textarea } from '../components/ui/textarea';
import AdminLayout from '../components/AdminLayout';
import axios from 'axios';

interface KYCRequest {
  verification_id: string;
  user_id: number;
  user_name: string;
  user_email: string;
  id_number: string;
  full_name: string;
  date_of_birth: string;
  gender: string;
  nationality: string;
  front_image: string;
  back_image: string;
  face_image: string;
  issue_date?: string;
  expiry_date?: string;
  status: 'pending' | 'verified' | 'rejected';
  submitted_at: string;
  verified_at?: string;
  rejection_reason?: string;
}

export default function AdminKYCManagement() {
  const [kycRequests, setKycRequests] = useState<KYCRequest[]>([]);
  const [filteredRequests, setFilteredRequests] = useState<KYCRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('pending');
  const [selectedRequest, setSelectedRequest] = useState<KYCRequest | null>(null);
  const [showDetailModal, setShowDetailModal] = useState(false);
  const [showImageModal, setShowImageModal] = useState(false);
  const [selectedImage, setSelectedImage] = useState('');
  const [imageTitle, setImageTitle] = useState('');
  const [rejectionReason, setRejectionReason] = useState('');
  const [processing, setProcessing] = useState(false);

  useEffect(() => {
    fetchKYCRequests();
  }, []);

  useEffect(() => {
    filterRequests();
  }, [kycRequests, searchTerm, statusFilter]);

  const fetchKYCRequests = async () => {
    try {
      const response = await axios.get('/api/admin/kyc/requests');
      
      if (response.data.success) {
        setKycRequests(response.data.requests);
      }
    } catch (error) {
      console.error('Error fetching KYC requests:', error);
    } finally {
      setLoading(false);
    }
  };

  const filterRequests = () => {
    let filtered = kycRequests;

    if (searchTerm) {
      filtered = filtered.filter(req => 
        req.user_name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        req.user_email.toLowerCase().includes(searchTerm.toLowerCase()) ||
        req.id_number.includes(searchTerm) ||
        req.full_name.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (statusFilter !== 'all') {
      filtered = filtered.filter(req => req.status === statusFilter);
    }

    setFilteredRequests(filtered);
  };

  const handleViewDetail = (request: KYCRequest) => {
    setSelectedRequest(request);
    setShowDetailModal(true);
    setRejectionReason('');
  };

  const handleViewImage = (imageData: string, title: string) => {
    setSelectedImage(imageData);
    setImageTitle(title);
    setShowImageModal(true);
  };

  const handleApprove = async () => {
    if (!selectedRequest) return;
    if (!confirm('Xác nhận duyệt KYC cho người dùng này?')) return;

    setProcessing(true);
    try {
      const response = await axios.post(
        `/api/admin/kyc/approve/${selectedRequest.verification_id}`
      );

      if (response.data.success) {
        alert('✅ Đã duyệt KYC thành công!');
        setShowDetailModal(false);
        fetchKYCRequests();
      }
    } catch (error) {
      console.error('Error approving KYC:', error);
      alert('❌ Lỗi khi duyệt KYC');
    } finally {
      setProcessing(false);
    }
  };

  const handleReject = async () => {
    if (!selectedRequest) return;
    if (!rejectionReason.trim()) {
      alert('Vui lòng nhập lý do từ chối');
      return;
    }
    if (!confirm('Xác nhận từ chối KYC cho người dùng này?')) return;

    setProcessing(true);
    try {
      const response = await axios.post(
        `/api/admin/kyc/reject/${selectedRequest.verification_id}`,
        { reason: rejectionReason }
      );

      if (response.data.success) {
        alert('✅ Đã từ chối KYC!');
        setShowDetailModal(false);
        fetchKYCRequests();
      }
    } catch (error) {
      console.error('Error rejecting KYC:', error);
      alert('❌ Lỗi khi từ chối KYC');
    } finally {
      setProcessing(false);
    }
  };

  const formatDate = (date: string) => {
    return new Date(date).toLocaleDateString('vi-VN');
  };

  const formatDateTime = (date: string) => {
    return new Date(date).toLocaleString('vi-VN');
  };

  const getStatusBadge = (status: string) => {
    const config = {
      pending: { color: 'bg-yellow-100 text-yellow-800', icon: Clock, label: 'Chờ duyệt' },
      verified: { color: 'bg-green-100 text-green-800', icon: Check, label: 'Đã duyệt' },
      rejected: { color: 'bg-red-100 text-red-800', icon: X, label: 'Từ chối' }
    };
    
    const item = config[status as keyof typeof config] || config.pending;
    const Icon = item.icon;
    
    return (
      <Badge className={item.color}>
        <Icon className="w-3 h-3 mr-1" />
        {item.label}
      </Badge>
    );
  };

  const stats = {
    total: kycRequests.length,
    pending: kycRequests.filter(r => r.status === 'pending').length,
    verified: kycRequests.filter(r => r.status === 'verified').length,
    rejected: kycRequests.filter(r => r.status === 'rejected').length
  };

  if (loading) {
    return (
      <AdminLayout>
        <div className="flex items-center justify-center h-96">
          <div className="text-center">
            <div className="w-16 h-16 border-4 border-blue-600 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
            <p className="text-gray-600">Đang tải dữ liệu...</p>
          </div>
        </div>
      </AdminLayout>
    );
  }

  return (
    <AdminLayout>
      <div className="max-w-7xl mx-auto">
        {/* Header */}
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-gray-900 mb-2">Quản lý xác thực KYC</h1>
          <p className="text-gray-600">Duyệt và xác minh danh tính người dùng</p>
        </div>

        {/* Stats Cards */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-6 mb-8">
          <Card>
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-600">Tổng yêu cầu</p>
                  <p className="text-2xl font-bold text-gray-900">{stats.total}</p>
                </div>
                <FileText className="w-10 h-10 text-blue-600" />
              </div>
            </CardContent>
          </Card>
          
          <Card>
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-600">Chờ duyệt</p>
                  <p className="text-2xl font-bold text-yellow-600">{stats.pending}</p>
                </div>
                <Clock className="w-10 h-10 text-yellow-600" />
              </div>
            </CardContent>
          </Card>
          
          <Card>
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-600">Đã duyệt</p>
                  <p className="text-2xl font-bold text-green-600">{stats.verified}</p>
                </div>
                <Check className="w-10 h-10 text-green-600" />
              </div>
            </CardContent>
          </Card>
          
          <Card>
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-600">Đã từ chối</p>
                  <p className="text-2xl font-bold text-red-600">{stats.rejected}</p>
                </div>
                <X className="w-10 h-10 text-red-600" />
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Filters */}
        <Card className="mb-6">
          <CardContent className="p-6">
            <div className="flex flex-wrap gap-4">
              <div className="flex-1 min-w-[300px]">
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400 w-5 h-5" />
                  <Input
                    placeholder="Tìm kiếm theo tên, email, CCCD..."
                    value={searchTerm}
                    onChange={(e) => setSearchTerm(e.target.value)}
                    className="pl-10"
                  />
                </div>
              </div>
              
              <Select value={statusFilter} onValueChange={setStatusFilter}>
                <SelectTrigger className="w-[200px]">
                  <Filter className="w-4 h-4 mr-2" />
                  <SelectValue placeholder="Trạng thái" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">Tất cả</SelectItem>
                  <SelectItem value="pending">Chờ duyệt</SelectItem>
                  <SelectItem value="verified">Đã duyệt</SelectItem>
                  <SelectItem value="rejected">Đã từ chối</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </CardContent>
        </Card>

        {/* KYC Requests Table */}
        <Card>
          <CardHeader>
            <CardTitle>Danh sách yêu cầu ({filteredRequests.length})</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="border-b">
                    <th className="text-left p-3">Người dùng</th>
                    <th className="text-left p-3">Họ tên CCCD</th>
                    <th className="text-left p-3">Số CCCD</th>
                    <th className="text-left p-3">Ngày sinh</th>
                    <th className="text-left p-3">Trạng thái</th>
                    <th className="text-left p-3">Ngày gửi</th>
                    <th className="text-left p-3">Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredRequests.map((request) => (
                    <tr key={request.verification_id} className="border-b hover:bg-gray-50">
                      <td className="p-3">
                        <div>
                          <div className="font-medium">{request.user_name}</div>
                          <div className="text-xs text-gray-500">{request.user_email}</div>
                        </div>
                      </td>
                      <td className="p-3 font-medium">{request.full_name}</td>
                      <td className="p-3 font-mono">{request.id_number}</td>
                      <td className="p-3">{formatDate(request.date_of_birth)}</td>
                      <td className="p-3">{getStatusBadge(request.status)}</td>
                      <td className="p-3 text-sm text-gray-600">{formatDateTime(request.submitted_at)}</td>
                      <td className="p-3">
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => handleViewDetail(request)}
                        >
                          <Eye className="w-4 h-4 mr-2" />
                          Chi tiết
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              
              {filteredRequests.length === 0 && (
                <div className="text-center py-12 text-gray-500">
                  <ShieldCheck className="w-16 h-16 mx-auto mb-4 opacity-50" />
                  <p>Không tìm thấy yêu cầu KYC nào</p>
                </div>
              )}
            </div>
          </CardContent>
        </Card>

        {/* Detail Modal */}
        <Dialog open={showDetailModal} onOpenChange={setShowDetailModal}>
          <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
            <DialogHeader>
              <DialogTitle>Chi tiết xác thực KYC</DialogTitle>
            </DialogHeader>
            
            {selectedRequest && (
              <div className="space-y-6">
                {/* Status Banner */}
                <div className={`p-4 rounded-lg ${
                  selectedRequest.status === 'pending' ? 'bg-yellow-50 border border-yellow-200' :
                  selectedRequest.status === 'verified' ? 'bg-green-50 border border-green-200' :
                  'bg-red-50 border border-red-200'
                }`}>
                  <div className="flex items-center gap-3">
                    {selectedRequest.status === 'pending' ? <Clock className="w-6 h-6 text-yellow-600" /> :
                     selectedRequest.status === 'verified' ? <Check className="w-6 h-6 text-green-600" /> :
                     <X className="w-6 h-6 text-red-600" />}
                    <div>
                      <h4 className="font-semibold">
                        {selectedRequest.status === 'pending' ? 'Đang chờ xét duyệt' :
                         selectedRequest.status === 'verified' ? 'Đã được xác thực' :
                         'Đã bị từ chối'}
                      </h4>
                      <p className="text-sm opacity-80">
                        Gửi lúc: {formatDateTime(selectedRequest.submitted_at)}
                      </p>
                    </div>
                  </div>
                </div>

                {/* User Info */}
                <div className="bg-gray-50 p-4 rounded-lg">
                  <h4 className="font-semibold mb-3 flex items-center gap-2">
                    <User className="w-5 h-5" />
                    Thông tin tài khoản
                  </h4>
                  <div className="grid grid-cols-2 gap-4 text-sm">
                    <div>
                      <p className="text-gray-600">Tên người dùng</p>
                      <p className="font-medium">{selectedRequest.user_name}</p>
                    </div>
                    <div>
                      <p className="text-gray-600">Email</p>
                      <p className="font-medium">{selectedRequest.user_email}</p>
                    </div>
                    <div>
                      <p className="text-gray-600">User ID</p>
                      <p className="font-medium">#{selectedRequest.user_id}</p>
                    </div>
                  </div>
                </div>

                {/* CCCD Info */}
                <div className="bg-blue-50 p-4 rounded-lg">
                  <h4 className="font-semibold mb-3 flex items-center gap-2">
                    <FileText className="w-5 h-5" />
                    Thông tin CCCD
                  </h4>
                  <div className="grid grid-cols-2 gap-4 text-sm">
                    <div>
                      <p className="text-gray-600">Họ và tên</p>
                      <p className="font-medium">{selectedRequest.full_name}</p>
                    </div>
                    <div>
                      <p className="text-gray-600">Số CCCD</p>
                      <p className="font-mono font-medium">{selectedRequest.id_number}</p>
                    </div>
                    <div>
                      <p className="text-gray-600">Ngày sinh</p>
                      <p className="font-medium">{formatDate(selectedRequest.date_of_birth)}</p>
                    </div>
                    <div>
                      <p className="text-gray-600">Giới tính</p>
                      <p className="font-medium">{selectedRequest.gender}</p>
                    </div>
                    <div>
                      <p className="text-gray-600">Quốc tịch</p>
                      <p className="font-medium">{selectedRequest.nationality}</p>
                    </div>
                    {selectedRequest.expiry_date && (
                      <div>
                        <p className="text-gray-600">Ngày hết hạn</p>
                        <p className="font-medium">{formatDate(selectedRequest.expiry_date)}</p>
                      </div>
                    )}
                  </div>
                </div>

                {/* Images */}
                <div>
                  <h4 className="font-semibold mb-3 flex items-center gap-2">
                    <Image className="w-5 h-5" />
                    Ảnh xác thực
                  </h4>
                  <div className="grid grid-cols-3 gap-4">
                    <div>
                      <p className="text-sm text-gray-600 mb-2">Mặt trước CCCD</p>
                      <div 
                        className="relative border-2 border-gray-200 rounded-lg overflow-hidden cursor-pointer hover:border-blue-500 transition-colors group"
                        onClick={() => handleViewImage(selectedRequest.front_image, 'Mặt trước CCCD')}
                      >
                        <img 
                          src={selectedRequest.front_image} 
                          alt="Front" 
                          className="w-full h-48 object-cover"
                        />
                        <div className="absolute inset-0 bg-black bg-opacity-0 group-hover:bg-opacity-30 transition-all flex items-center justify-center">
                          <ZoomIn className="w-8 h-8 text-white opacity-0 group-hover:opacity-100 transition-opacity" />
                        </div>
                      </div>
                    </div>
                    
                    <div>
                      <p className="text-sm text-gray-600 mb-2">Mặt sau CCCD</p>
                      <div 
                        className="relative border-2 border-gray-200 rounded-lg overflow-hidden cursor-pointer hover:border-blue-500 transition-colors group"
                        onClick={() => handleViewImage(selectedRequest.back_image, 'Mặt sau CCCD')}
                      >
                        <img 
                          src={selectedRequest.back_image} 
                          alt="Back" 
                          className="w-full h-48 object-cover"
                        />
                        <div className="absolute inset-0 bg-black bg-opacity-0 group-hover:bg-opacity-30 transition-all flex items-center justify-center">
                          <ZoomIn className="w-8 h-8 text-white opacity-0 group-hover:opacity-100 transition-opacity" />
                        </div>
                      </div>
                    </div>
                    
                    <div>
                      <p className="text-sm text-gray-600 mb-2">Ảnh khuôn mặt</p>
                      <div 
                        className="relative border-2 border-gray-200 rounded-lg overflow-hidden cursor-pointer hover:border-blue-500 transition-colors group"
                        onClick={() => handleViewImage(selectedRequest.face_image, 'Ảnh khuôn mặt')}
                      >
                        <img 
                          src={selectedRequest.face_image} 
                          alt="Face" 
                          className="w-full h-48 object-cover"
                        />
                        <div className="absolute inset-0 bg-black bg-opacity-0 group-hover:bg-opacity-30 transition-all flex items-center justify-center">
                          <ZoomIn className="w-8 h-8 text-white opacity-0 group-hover:opacity-100 transition-opacity" />
                        </div>
                      </div>
                    </div>
                  </div>
                </div>

                {/* Rejection Reason */}
                {selectedRequest.status === 'rejected' && selectedRequest.rejection_reason && (
                  <div className="bg-red-50 border border-red-200 p-4 rounded-lg">
                    <h4 className="font-semibold text-red-900 mb-2 flex items-center gap-2">
                      <AlertCircle className="w-5 h-5" />
                      Lý do từ chối
                    </h4>
                    <p className="text-red-800">{selectedRequest.rejection_reason}</p>
                  </div>
                )}

                {/* Actions for pending requests */}
                {selectedRequest.status === 'pending' && (
                  <div className="space-y-4 border-t pt-4">
                    <div>
                      <Label>Lý do từ chối (nếu từ chối)</Label>
                      <Textarea
                        value={rejectionReason}
                        onChange={(e) => setRejectionReason(e.target.value)}
                        placeholder="Nhập lý do từ chối KYC..."
                        rows={3}
                      />
                    </div>
                    
                    <div className="flex gap-3">
                      <Button
                        onClick={handleReject}
                        disabled={processing || !rejectionReason.trim()}
                        variant="outline"
                        className="flex-1 border-red-500 text-red-600 hover:bg-red-50"
                      >
                        {processing ? (
                          <>
                            <div className="w-4 h-4 border-2 border-red-600 border-t-transparent rounded-full animate-spin mr-2" />
                            Đang xử lý...
                          </>
                        ) : (
                          <>
                            <X className="w-4 h-4 mr-2" />
                            Từ chối
                          </>
                        )}
                      </Button>
                      
                      <Button
                        onClick={handleApprove}
                        disabled={processing}
                        className="flex-1 bg-green-600 hover:bg-green-700"
                      >
                        {processing ? (
                          <>
                            <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin mr-2" />
                            Đang xử lý...
                          </>
                        ) : (
                          <>
                            <Check className="w-4 h-4 mr-2" />
                            Duyệt KYC
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

        {/* Image Viewer Modal */}
        <Dialog open={showImageModal} onOpenChange={setShowImageModal}>
          <DialogContent className="max-w-5xl">
            <DialogHeader>
              <DialogTitle>{imageTitle}</DialogTitle>
            </DialogHeader>
            <div className="flex items-center justify-center p-4">
              <img 
                src={selectedImage} 
                alt={imageTitle}
                className="max-w-full max-h-[70vh] object-contain rounded-lg"
              />
            </div>
            <div className="flex justify-center gap-2">
              <Button variant="outline" onClick={() => setShowImageModal(false)}>
                Đóng
              </Button>
              <Button onClick={() => {
                const link = document.createElement('a');
                link.href = selectedImage;
                link.download = `${imageTitle}_${Date.now()}.jpg`;
                link.click();
              }}>
                <Download className="w-4 h-4 mr-2" />
                Tải xuống
              </Button>
            </div>
          </DialogContent>
        </Dialog>
      </div>
      </AdminLayout>
  );
}

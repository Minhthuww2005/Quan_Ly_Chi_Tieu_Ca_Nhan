/**
 * Admin Transaction Management - Monitor all transactions
 */

import { useState, useEffect } from 'react';
import { 
  ArrowLeftRight, 
  Search, 
  Download, 
  Eye,
  RefreshCw,
  TrendingUp,
  CheckCircle,
  XCircle,
  Clock,
  DollarSign
} from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { Badge } from '../components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../components/ui/select';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '../components/ui/dialog';
import AdminLayout from '../components/AdminLayout';
import axios from 'axios';

interface Transaction {
  transaction_id: string;
  sender_id: number;
  recipient_id: number;
  sender_name: string;
  recipient_name: string;
  amount: number;
  fee: number;
  type: string;
  status: string;
  description?: string;
  created_at: string;
  updated_at: string;
}

interface TransactionStats {
  total: number;
  completed: number;
  pending: number;
  failed: number;
  totalVolume: number;
  totalFees: number;
  todayTransactions: number;
  todayVolume: number;
}

export default function AdminTransactionManagement() {
  const [transactions, setTransactions] = useState<Transaction[]>([]);
  const [filteredTransactions, setFilteredTransactions] = useState<Transaction[]>([]);
  const [stats, setStats] = useState<TransactionStats>({
    total: 0,
    completed: 0,
    pending: 0,
    failed: 0,
    totalVolume: 0,
    totalFees: 0,
    todayTransactions: 0,
    todayVolume: 0
  });
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [typeFilter, setTypeFilter] = useState('all');
  const [dateFrom, setDateFrom] = useState('');
  const [selectedTransaction, setSelectedTransaction] = useState<Transaction | null>(null);
  const [showDetailModal, setShowDetailModal] = useState(false);

  useEffect(() => {
    fetchTransactions();
  }, []);

  useEffect(() => {
    filterTransactions();
  }, [transactions, searchTerm, statusFilter, typeFilter, dateFrom]);

  const fetchTransactions = async () => {
    try {
      const response = await axios.get('/api/admin/transactions');
      
      if (response.data.success) {
        setTransactions(response.data.transactions || []);
        setStats(response.data.stats || {
          total: 0,
          completed: 0,
          pending: 0,
          failed: 0,
          totalVolume: 0,
          totalFees: 0,
          todayTransactions: 0,
          todayVolume: 0
        });
      }
    } catch (error) {
      console.error('Error fetching transactions:', error);
    } finally {
      setLoading(false);
    }
  };

  const filterTransactions = () => {
    let filtered = transactions;

    if (searchTerm) {
      filtered = filtered.filter(tx => 
        tx.transaction_id.toLowerCase().includes(searchTerm.toLowerCase()) ||
        tx.sender_name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        tx.recipient_name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        (tx.description && tx.description.toLowerCase().includes(searchTerm.toLowerCase()))
      );
    }

    if (statusFilter !== 'all') {
      filtered = filtered.filter(tx => tx.status === statusFilter);
    }

    if (typeFilter !== 'all') {
      filtered = filtered.filter(tx => tx.type === typeFilter);
    }

    if (dateFrom) {
      filtered = filtered.filter(tx => new Date(tx.created_at) >= new Date(dateFrom));
    }

    setFilteredTransactions(filtered);
  };

  const handleExport = () => {
    const csv = [
      ['ID', 'Người gửi', 'Người nhận', 'Số tiền', 'Phí', 'Loại', 'Trạng thái', 'Ngày tạo'].join(','),
      ...filteredTransactions.map(tx => [
        tx.transaction_id,
        tx.sender_name,
        tx.recipient_name,
        tx.amount,
        tx.fee,
        tx.type,
        tx.status,
        new Date(tx.created_at).toLocaleString('vi-VN')
      ].join(','))
    ].join('\n');

    const blob = new Blob([csv], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `transactions_${new Date().toISOString()}.csv`;
    a.click();
  };

  const handleViewDetail = (transaction: Transaction) => {
    setSelectedTransaction(transaction);
    setShowDetailModal(true);
  };

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('vi-VN').format(amount) + ' VNĐ';
  };

  const formatDate = (date: string) => {
    return new Date(date).toLocaleString('vi-VN');
  };

  const getStatusBadge = (status: string) => {
    const config = {
      completed: { color: 'bg-green-100 text-green-800', icon: CheckCircle, label: 'Thành công' },
      pending: { color: 'bg-yellow-100 text-yellow-800', icon: Clock, label: 'Đang xử lý' },
      failed: { color: 'bg-red-100 text-red-800', icon: XCircle, label: 'Thất bại' }
    };
    
    const config_item = config[status as keyof typeof config] || config.pending;
    const Icon = config_item.icon;
    
    return (
      <Badge className={config_item.color}>
        <Icon className="w-3 h-3 mr-1" />
        {config_item.label}
      </Badge>
    );
  };

  const getTypeBadge = (type: string) => {
    const colors = {
      transfer: 'bg-blue-100 text-blue-800',
      deposit: 'bg-green-100 text-green-800',
      withdrawal: 'bg-orange-100 text-orange-800',
      payment: 'bg-purple-100 text-purple-800'
    };
    
    const labels = {
      transfer: 'Chuyển khoản',
      deposit: 'Nạp tiền',
      withdrawal: 'Rút tiền',
      payment: 'Thanh toán'
    };
    
    return (
      <Badge className={colors[type as keyof typeof colors] || 'bg-gray-100 text-gray-800'}>
        {labels[type as keyof typeof labels] || type}
      </Badge>
    );
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
        <div className="mb-8 flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold text-gray-900 mb-2">Quản lý giao dịch</h1>
            <p className="text-gray-600">Tổng cộng {transactions.length} giao dịch</p>
          </div>
          <div className="flex gap-2">
            <Button variant="outline" onClick={fetchTransactions}>
              <RefreshCw className="w-4 h-4 mr-2" />
              Làm mới
            </Button>
            <Button onClick={handleExport}>
              <Download className="w-4 h-4 mr-2" />
              Xuất CSV
            </Button>
          </div>
        </div>

        {/* Stats Cards */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-6 mb-8">
          <Card>
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-600">Tổng giao dịch</p>
                  <p className="text-2xl font-bold text-gray-900">{stats?.total || 0}</p>
                </div>
                <ArrowLeftRight className="w-10 h-10 text-blue-600" />
              </div>
            </CardContent>
          </Card>
          
          <Card>
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-600">Thành công</p>
                  <p className="text-2xl font-bold text-green-600">{stats?.completed || 0}</p>
                </div>
                <CheckCircle className="w-10 h-10 text-green-600" />
              </div>
            </CardContent>
          </Card>
          
          <Card>
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-600">Tổng khối lượng</p>
                  <p className="text-xl font-bold text-purple-600">{formatCurrency(stats?.totalVolume || 0)}</p>
                </div>
                <DollarSign className="w-10 h-10 text-purple-600" />
              </div>
            </CardContent>
          </Card>
          
          <Card>
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-600">Tổng phí</p>
                  <p className="text-xl font-bold text-orange-600">{formatCurrency(stats?.totalFees || 0)}</p>
                </div>
                <TrendingUp className="w-10 h-10 text-orange-600" />
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Filters */}
        <Card className="mb-6">
          <CardContent className="p-6">
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4">
              <div className="lg:col-span-2">
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400 w-5 h-5" />
                  <Input
                    placeholder="Tìm kiếm ID, người gửi, người nhận..."
                    value={searchTerm}
                    onChange={(e) => setSearchTerm(e.target.value)}
                    className="pl-10"
                  />
                </div>
              </div>
              
              <Select value={statusFilter} onValueChange={setStatusFilter}>
                <SelectTrigger>
                  <SelectValue placeholder="Trạng thái" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">Tất cả</SelectItem>
                  <SelectItem value="completed">Thành công</SelectItem>
                  <SelectItem value="pending">Đang xử lý</SelectItem>
                  <SelectItem value="failed">Thất bại</SelectItem>
                </SelectContent>
              </Select>

              <Select value={typeFilter} onValueChange={setTypeFilter}>
                <SelectTrigger>
                  <SelectValue placeholder="Loại giao dịch" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">Tất cả</SelectItem>
                  <SelectItem value="transfer">Chuyển khoản</SelectItem>
                  <SelectItem value="deposit">Nạp tiền</SelectItem>
                  <SelectItem value="withdrawal">Rút tiền</SelectItem>
                  <SelectItem value="payment">Thanh toán</SelectItem>
                </SelectContent>
              </Select>

              <div className="flex gap-2">
                <Input
                  type="date"
                  value={dateFrom}
                  onChange={(e) => setDateFrom(e.target.value)}
                  placeholder="Từ ngày"
                />
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Transactions Table */}
        <Card>
          <CardHeader>
            <CardTitle>Danh sách giao dịch ({filteredTransactions.length})</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="border-b">
                    <th className="text-left p-3">ID</th>
                    <th className="text-left p-3">Người gửi</th>
                    <th className="text-left p-3">Người nhận</th>
                    <th className="text-right p-3">Số tiền</th>
                    <th className="text-right p-3">Phí</th>
                    <th className="text-left p-3">Loại</th>
                    <th className="text-left p-3">Trạng thái</th>
                    <th className="text-left p-3">Thời gian</th>
                    <th className="text-left p-3">Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredTransactions.map((tx, index) => (
                    <tr key={tx.transaction_id || `tx-${index}`} className="border-b hover:bg-gray-50">
                      <td className="p-3 font-mono text-sm">{tx.transaction_id ? tx.transaction_id.substring(0, 8) + '...' : 'N/A'}</td>
                      <td className="p-3">
                        <div>
                          <div className="font-medium">{tx.sender_name}</div>
                          <div className="text-xs text-gray-500">ID: {tx.sender_id}</div>
                        </div>
                      </td>
                      <td className="p-3">
                        <div>
                          <div className="font-medium">{tx.recipient_name}</div>
                          <div className="text-xs text-gray-500">ID: {tx.recipient_id}</div>
                        </div>
                      </td>
                      <td className="p-3 text-right font-semibold">{formatCurrency(tx.amount)}</td>
                      <td className="p-3 text-right text-gray-600">{formatCurrency(tx.fee)}</td>
                      <td className="p-3">{getTypeBadge(tx.type)}</td>
                      <td className="p-3">{getStatusBadge(tx.status)}</td>
                      <td className="p-3 text-sm text-gray-600">{formatDate(tx.created_at)}</td>
                      <td className="p-3">
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => handleViewDetail(tx)}
                        >
                          <Eye className="w-4 h-4" />
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              
              {filteredTransactions.length === 0 && (
                <div className="text-center py-12 text-gray-500">
                  <ArrowLeftRight className="w-16 h-16 mx-auto mb-4 opacity-50" />
                  <p>Không tìm thấy giao dịch nào</p>
                </div>
              )}
            </div>
          </CardContent>
        </Card>

        {/* Transaction Detail Modal */}
        <Dialog open={showDetailModal} onOpenChange={setShowDetailModal}>
          <DialogContent className="max-w-2xl">
            <DialogHeader>
              <DialogTitle>Chi tiết giao dịch</DialogTitle>
            </DialogHeader>
            
            {selectedTransaction && (
              <div className="space-y-4">
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <p className="text-sm text-gray-600">ID giao dịch</p>
                    <p className="font-mono text-sm">{selectedTransaction.transaction_id}</p>
                  </div>
                  <div>
                    <p className="text-sm text-gray-600">Trạng thái</p>
                    {getStatusBadge(selectedTransaction.status)}
                  </div>
                </div>

                <div className="border-t pt-4">
                  <h4 className="font-semibold mb-3">Thông tin người gửi</h4>
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <p className="text-sm text-gray-600">Tên</p>
                      <p className="font-medium">{selectedTransaction.sender_name}</p>
                    </div>
                    <div>
                      <p className="text-sm text-gray-600">ID</p>
                      <p className="font-medium">{selectedTransaction.sender_id}</p>
                    </div>
                  </div>
                </div>

                <div className="border-t pt-4">
                  <h4 className="font-semibold mb-3">Thông tin người nhận</h4>
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <p className="text-sm text-gray-600">Tên</p>
                      <p className="font-medium">{selectedTransaction.recipient_name}</p>
                    </div>
                    <div>
                      <p className="text-sm text-gray-600">ID</p>
                      <p className="font-medium">{selectedTransaction.recipient_id}</p>
                    </div>
                  </div>
                </div>

                <div className="border-t pt-4">
                  <h4 className="font-semibold mb-3">Thông tin giao dịch</h4>
                  <div className="space-y-3">
                    <div className="flex justify-between">
                      <span className="text-gray-600">Số tiền:</span>
                      <span className="font-semibold text-lg">{formatCurrency(selectedTransaction.amount)}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-gray-600">Phí giao dịch:</span>
                      <span className="font-medium">{formatCurrency(selectedTransaction.fee)}</span>
                    </div>
                    <div className="flex justify-between border-t pt-2">
                      <span className="font-medium">Tổng:</span>
                      <span className="font-bold text-lg">{formatCurrency(selectedTransaction.amount + selectedTransaction.fee)}</span>
                    </div>
                  </div>
                </div>

                {selectedTransaction.description && (
                  <div className="border-t pt-4">
                    <p className="text-sm text-gray-600 mb-1">Ghi chú</p>
                    <p className="text-gray-900">{selectedTransaction.description}</p>
                  </div>
                )}

                <div className="border-t pt-4">
                  <div className="grid grid-cols-2 gap-4 text-sm">
                    <div>
                      <p className="text-gray-600">Loại giao dịch</p>
                      {getTypeBadge(selectedTransaction.type)}
                    </div>
                    <div>
                      <p className="text-gray-600">Thời gian tạo</p>
                      <p className="font-medium">{formatDate(selectedTransaction.created_at)}</p>
                    </div>
                  </div>
                </div>
              </div>
            )}
          </DialogContent>
        </Dialog>
      </div>
      </AdminLayout>
  );
}

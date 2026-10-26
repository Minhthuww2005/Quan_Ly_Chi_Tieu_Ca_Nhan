import { useState, useEffect } from 'react';
import { ArrowLeft, Search, ArrowUpRight, ArrowDownLeft, CreditCard, Smartphone } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Input } from '../components/ui/input';
import { Card, CardContent } from '../components/ui/card';
import { Badge } from '../components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../components/ui/select';
import BottomNavigation from '../components/BottomNavigation';
import { transactionAPI } from '../services/api';

interface Transaction {
  trans_id?: number;
  wallet_id?: number;
  type: string;
  amount: number;
  title?: string;
  description?: string;
  status: string;
  timestamp?: string | Date;
  icon?: string;
  color?: string;
  recipient?: string;
  reference?: string;
  failure_reason?: string;
}

export default function HistoryPage() {
  const navigate = useNavigate();
  const [searchTerm, setSearchTerm] = useState('');
  const [filterType, setFilterType] = useState('all');
  const [filterStatus, setFilterStatus] = useState('all');
  const [transactions, setTransactions] = useState<Transaction[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchTransactions = async () => {
      try {
        const userInfo = localStorage.getItem('userInfo');
        if (!userInfo) {
          navigate('/');
          return;
        }

        const user = JSON.parse(userInfo);
        const response = await transactionAPI.getTransactions(user.user_id, 100); // Get all
        
        if (response.success) {
          setTransactions(response.transactions || []);
        }
      } catch (error) {
        console.error('Error fetching transactions:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchTransactions();
  }, [navigate]);

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('vi-VN').format(amount) + ' VNĐ';
  };

  const formatDate = (dateString: string | Date | undefined) => {
    if (!dateString) return 'Không xác định';
    const date = new Date(dateString);
    if (isNaN(date.getTime())) return 'Không xác định';
    return date.toLocaleDateString('vi-VN', { 
      day: '2-digit', 
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  const getTransactionIcon = (type: string) => {
    const iconClass = "w-5 h-5";
    switch(type) {
      case 'deposit':
      case 'receive':
        return <ArrowDownLeft className={`${iconClass} text-green-600`} />;
      case 'transfer':
      case 'send':
        return <ArrowUpRight className={`${iconClass} text-red-600`} />;
      case 'payment':
        return <CreditCard className={`${iconClass} text-blue-600`} />;
      default:
        return <Smartphone className={`${iconClass} text-gray-600`} />;
    }
  };

  const getAmountColor = (amount: number) => {
    return amount >= 0 ? 'text-green-700' : 'text-red-700';
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'success': return 'bg-green-100 text-green-800';
      case 'pending': return 'bg-yellow-100 text-yellow-800';
      case 'failed': return 'bg-red-100 text-red-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  const getStatusLabel = (status: string) => {
    switch (status) {
      case 'completed':
      case 'success': return 'Thành công';
      case 'pending': return 'Đang xử lý';
      case 'processing': return 'Đang xử lý';
      case 'failed': return 'Thất bại';
      default: return 'Không xác định';
    }
  };

  const getTypeLabel = (type: string) => {
    switch (type) {
      case 'deposit': return 'Nạp tiền';
      case 'withdrawal': return 'Rút tiền';
      case 'transfer': return 'Chuyển khoản';
      case 'payment': return 'Thanh toán';
      case 'loan': return 'Vay vốn';
      case 'investment': return 'Đầu tư';
      case 'insurance': return 'Bảo hiểm';
      case 'savings': return 'Tiết kiệm';
      case 'bank_transfer': return 'Chuyển đến ngân hàng';
      case 'bank_top_up': return 'Nạp từ ngân hàng';
      case 'phone_top_up': return 'Nạp điện thoại';
      case 'bill_payment': return 'Thanh toán hóa đơn';
      case 'top_up': return 'Nạp tiền';
      case 'refund': return 'Hoàn tiền';
      case 'cashback': return 'Hoàn xu';
      default: return 'Khác';
    }
  };

  const filteredTransactions = transactions.filter(transaction => {
    const matchesSearch = (transaction.description || '').toLowerCase().includes(searchTerm.toLowerCase());
    const matchesType = filterType === 'all' || transaction.type === filterType;
    const matchesStatus = filterStatus === 'all' || transaction.status === filterStatus || 
                         (filterStatus === 'success' && transaction.status === 'completed');
    
    return matchesSearch && matchesType && matchesStatus;
  });

  const sortedTransactions = filteredTransactions.sort((a, b) => {
    const dateA = a.timestamp ? new Date(a.timestamp).getTime() : 0;
    const dateB = b.timestamp ? new Date(b.timestamp).getTime() : 0;
    return dateB - dateA;
  });

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <div className="bg-gradient-to-r from-gray-800 to-black px-6 py-8 text-white">
        <div className="flex items-center justify-between">
          <div className="flex items-center space-x-4">
            <button 
              onClick={() => navigate('/')}
              className="p-2 hover:bg-white/10 rounded-full transition-colors"
            >
              <ArrowLeft className="w-6 h-6" />
            </button>
            <div>
              <div className="text-xl font-bold">Lịch sử giao dịch</div>
              <div className="text-sm opacity-80">Tất cả giao dịch của bạn</div>
            </div>
          </div>
        </div>
      </div>

      {/* Search and Filters */}
      <div className="px-4 py-6 space-y-4">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-4 h-4 text-gray-400" />
          <Input
            placeholder="Tìm kiếm giao dịch..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="pl-10"
          />
        </div>

        <div className="flex space-x-3">
          <Select value={filterType} onValueChange={setFilterType}>
            <SelectTrigger className="flex-1">
              <SelectValue placeholder="Loại giao dịch" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Tất cả loại</SelectItem>
              <SelectItem value="deposit">Nạp tiền</SelectItem>
              <SelectItem value="transfer">Chuyển khoản</SelectItem>
              <SelectItem value="bank_transfer">Chuyển đến ngân hàng</SelectItem>
              <SelectItem value="phone_top_up">Nạp điện thoại</SelectItem>
              <SelectItem value="bill_payment">Thanh toán hóa đơn</SelectItem>
              <SelectItem value="payment">Thanh toán</SelectItem>
              <SelectItem value="loan">Vay vốn</SelectItem>
              <SelectItem value="investment">Đầu tư</SelectItem>
              <SelectItem value="insurance">Bảo hiểm</SelectItem>
              <SelectItem value="savings">Tiết kiệm</SelectItem>
            </SelectContent>
          </Select>

          <Select value={filterStatus} onValueChange={setFilterStatus}>
            <SelectTrigger className="flex-1">
              <SelectValue placeholder="Trạng thái" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Tất cả</SelectItem>
              <SelectItem value="success">Thành công</SelectItem>
              <SelectItem value="pending">Đang xử lý</SelectItem>
              <SelectItem value="failed">Thất bại</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>

      {/* Transaction List */}
      <div className="px-4 pb-24 space-y-3">
        {loading ? (
          <div className="space-y-3">
            {[1, 2, 3, 4, 5].map((i) => (
              <div key={i} className="h-24 bg-gray-200 rounded-xl animate-pulse"></div>
            ))}
          </div>
        ) : sortedTransactions.length === 0 ? (
          <Card>
            <CardContent className="p-8 text-center">
              <div className="text-gray-500 mb-2">Không tìm thấy giao dịch nào</div>
              <div className="text-sm text-gray-400">Thử thay đổi bộ lọc hoặc từ khóa tìm kiếm</div>
            </CardContent>
          </Card>
        ) : (
          sortedTransactions.map((transaction) => (
            <Card key={transaction.trans_id || Math.random()} className="hover:shadow-md transition-shadow cursor-pointer">
              <CardContent className="p-4">
                <div className="flex items-center space-x-4">
                  <div className="w-12 h-12 bg-gray-100 rounded-xl flex items-center justify-center flex-shrink-0">
                    {getTransactionIcon(transaction.type)}
                  </div>
                  
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center space-x-2 mb-1">
                      <div className="font-medium text-gray-900 text-sm">
                        {transaction.title || getTypeLabel(transaction.type)}
                      </div>
                      <Badge variant="secondary" className="text-xs">
                        {getTypeLabel(transaction.type)}
                      </Badge>
                    </div>
                    <div className="text-xs text-gray-500 leading-relaxed mb-2">
                      {transaction.description || 'Không có mô tả'}
                    </div>
                    {transaction.status === 'failed' && transaction.failure_reason && (
                      <div className="text-xs text-red-600 mb-2">Lý do: {transaction.failure_reason}</div>
                    )}
                    <div className="flex items-center justify-between">
                      <div className="text-xs text-gray-400">
                        {formatDate(transaction.timestamp)}
                      </div>
                      <Badge className={getStatusColor(transaction.status)}>
                        {getStatusLabel(transaction.status)}
                      </Badge>
                    </div>
                  </div>
                  
                  <div className="text-right flex-shrink-0">
                    {/* A failed operation moved no money: the amount is shown struck through. */}
                    <div className={`font-semibold text-sm ${transaction.status === 'failed' ? 'text-gray-400 line-through' : getAmountColor(transaction.amount)}`}>
                      {transaction.amount > 0 ? '+' : ''}
                      {formatCurrency(transaction.amount)}
                    </div>
                  </div>
                </div>
              </CardContent>
            </Card>
          ))
        )}
      </div>

      <BottomNavigation />
    </div>
  );
}

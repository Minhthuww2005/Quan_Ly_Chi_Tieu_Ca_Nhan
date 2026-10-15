import { useState, useEffect } from 'react';
import { ChevronRight, ArrowDownLeft, ArrowUpRight, CreditCard, Smartphone } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { transactionAPI } from '../services/api';

interface Transaction {
  trans_id: number;
  timestamp: string;
  title?: string;
  transaction_id: number;
  type: string;
  amount: number;
  description?: string;
  status: string;
  created_at: string;
  sender_id?: number;
  recipient_id?: number;
}

export default function TransactionList() {
  const navigate = useNavigate();
  const [transactions, setTransactions] = useState<Transaction[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchTransactions = async () => {
      try {
        const userInfo = localStorage.getItem('userInfo');
        if (!userInfo) {
          setLoading(false);
          return;
        }

        const user = JSON.parse(userInfo);
        if (!user.user_id) {
          console.error('Invalid user data');
          setLoading(false);
          return;
        }

        const response = await transactionAPI.getTransactions(user.user_id, 5);
        
        if (response.success) {
          setTransactions(response.transactions || []);
        }
      } catch (error: any) {
        console.error('Error fetching transactions:', error);
        // Không alert để tránh làm phiền user
      } finally {
        setLoading(false);
      }
    };

    fetchTransactions();
    
    // Reload transactions sau mỗi 30s
    const interval = setInterval(fetchTransactions, 30000);
    return () => clearInterval(interval);
  }, []);

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('vi-VN').format(amount) + ' VNĐ';
  };

  const getAmountColor = (amount: number) => {
    return amount >= 0 ? 'text-green-700' : 'text-red-700';
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

  const formatTransactionAmount = (amount: number, _type: string) => {
    const formatted = formatCurrency(Math.abs(amount));
    const isIncoming = amount >= 0;
    return isIncoming ? `+${formatted}` : `-${formatted}`;
  };

  const getTransactionTitle = (transaction: Transaction) => {
    if (transaction.title) return transaction.title;
    switch(transaction.type) {
      case 'transfer':
        return transaction.amount < 0 ? 'Chuyển tiền' : 'Nhận tiền';
      case 'deposit':
        return 'Nạp tiền';
      case 'payment':
        return 'Thanh toán';
      default:
        return 'Giao dịch';
    }
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString('vi-VN', { 
      day: '2-digit', 
      month: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  if (loading) {
    return (
      <div className="px-4 pb-24">
        <h2 className="text-lg font-semibold text-gray-900 mb-4">Giao dịch gần đây</h2>
        <div className="space-y-2">
          {[1, 2, 3].map((i) => (
            <div key={`skeleton-${i}`} className="h-20 bg-gray-100 rounded-xl animate-pulse"></div>
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="px-4 pb-24">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-lg font-semibold text-gray-900">Giao dịch gần đây</h2>
        <button 
          onClick={() => navigate('/history')}
          className="text-gray-600 text-sm font-medium flex items-center hover:text-gray-800 transition-colors"
        >
          Xem tất cả
          <ChevronRight className="w-4 h-4 ml-1" />
        </button>
      </div>
      
      <div className="space-y-2">
        {transactions.length === 0 ? (
          <div className="text-center py-8 text-gray-500">
            Chưa có giao dịch nào
          </div>
        ) : (
          transactions.map((transaction, index) => (
            <div
              key={`${transaction.transaction_id}-${index}`}
              className="flex items-center space-x-4 p-4 bg-white rounded-xl border border-gray-100 hover:border-gray-200 hover:shadow-sm transition-all duration-200 cursor-pointer"
            >
              <div className="w-12 h-12 bg-gray-100 rounded-xl flex items-center justify-center flex-shrink-0">
                {getTransactionIcon(transaction.type)}
              </div>
              
              <div className="flex-1 min-w-0">
                <div className="font-medium text-gray-900 text-sm mb-1">
                  {getTransactionTitle(transaction)}
                </div>
                <div className="text-xs text-gray-500 leading-relaxed">
                  {transaction.description || formatDate(transaction.timestamp || transaction.created_at)}
                </div>
              </div>
              
              <div className="text-right flex-shrink-0">
                <div className={`font-semibold text-sm ${getAmountColor(transaction.amount)}`}>
                  {formatTransactionAmount(transaction.amount, transaction.type)}
                </div>
                <div className="text-xs text-gray-400 mt-1">
                  {transaction.status === 'completed' || transaction.status === 'success' ? 'Thành công' :
                   transaction.status === 'pending' ? 'Đang xử lý' :
                   transaction.status === 'failed' ? 'Thất bại' : 'Chưa xác định'}
                </div>
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
}

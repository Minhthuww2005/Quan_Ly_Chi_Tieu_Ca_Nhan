import { useState, useEffect } from 'react';
import { ArrowLeft, Plus, CreditCard, Calendar, Building2, Calculator } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../components/ui/dialog';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { loanProduct, useFinancePolicy } from '../services/financePolicy';
import { useTransactionFlow } from '../components/TransactionFlow';

const LOAN_TERMS = [12, 24, 36, 48, 60, 84, 120, 180, 240, 300];
const DEFAULT_LOAN_TYPES = ['Vay tiêu dùng', 'Vay mua nhà', 'Vay mua xe', 'Vay kinh doanh'];

interface Loan {
  id: string;
  type: string;
  amount: number;
  remainingAmount: number;
  interestRate: number;
  term: number;
  monthlyPayment: number;
  startDate: string;
  nextPaymentDate: string;
  status: string;
  paidPercent: number;
}

export default function LoanPage() {
  const navigate = useNavigate();
  const flow = useTransactionFlow();
  const [loans, setLoans] = useState<Loan[]>([]);
  const [summary, setSummary] = useState({ totalBorrowed: 0, totalPaid: 0, totalRemaining: 0, activeCount: 0 });
  const [loading, setLoading] = useState(true);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [formData, setFormData] = useState({
    type: 'Vay tiêu dùng',
    amount: '',
    term: '12'
  });
  const { policy, error: policyError } = useFinancePolicy();
  const product = loanProduct(policy, formData.type);
  const termOptions = LOAN_TERMS.filter((t) => !product || t <= product.maxTermMonths);

  useEffect(() => {
    fetchLoans();
  }, []);

  const fetchLoans = async () => {
    try {
      const userInfo = JSON.parse(localStorage.getItem('userInfo') || '{}');
      const response = await fetch(`/api/loans/${userInfo.user_id}`);
      const data = await response.json();
      
      if (data.success) {
        setLoans(data.loans);
        setSummary(data.summary);
      }
    } catch (error) {
      console.error('Error fetching loans:', error);
    } finally {
      setLoading(false);
    }
  };

  // Review -> PIN -> result, the same steps as phone top-up; the list refreshes without reloading the page.
  const handleCreate = () => {
    const userInfo = JSON.parse(localStorage.getItem('userInfo') || '{}');
    const amount = parseFloat(formData.amount);
    const term = parseInt(formData.term);
    if (!(amount > 0)) {
      alert('Vui lòng nhập số tiền vay');
      return;
    }
    setShowCreateModal(false);
    flow.start({
      title: 'Đăng ký khoản vay',
      rows: [
        { label: 'Loại vay', value: formData.type },
        { label: 'Thời hạn', value: `${term} tháng` },
        { label: 'Lãi suất', value: `${product?.annualRate ?? '...'}%/năm` },
        { label: 'Tiền nhận vào ví', value: new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount), highlight: true },
      ],
      actionLabel: 'Nhận khoản vay',
      processingText: 'Đang giải ngân...',
      successText: 'Giải ngân khoản vay thành công!',
      run: async (pin) => {
        const response = await fetch('/api/loans/create', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ user_id: userInfo.user_id, pin, type: formData.type, amount, term })
        });
        const data = await response.json();
        if (!response.ok || !data.success) throw new Error(data.message || 'Không thể tạo khoản vay');
        setFormData({ type: 'Vay tiêu dùng', amount: '', term: '12' });
        fetchLoans();
      },
    });
  };

  const formatMoney = (amount: number) => {
    return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount);
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
          <p className="mt-4 text-gray-600">Đang tải...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <div className="bg-gradient-to-r from-orange-600 to-red-600 text-white">
        <div className="max-w-7xl mx-auto px-4 py-6">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-4">
              <button onClick={() => navigate(-1)} className="p-2 hover:bg-white/10 rounded-lg">
                <ArrowLeft className="w-6 h-6" />
              </button>
              <div>
                <h1 className="text-2xl font-bold">Hợp đồng vay</h1>
                <p className="text-orange-100 text-sm mt-1">Quản lý khoản vay</p>
              </div>
            </div>
            <Button onClick={() => setShowCreateModal(true)} className="bg-white text-orange-600 hover:bg-orange-50">
              <Plus className="w-5 h-5 mr-2" />
              Tạo khoản vay
            </Button>
          </div>
        </div>
      </div>

      <div className="max-w-7xl mx-auto px-4 py-8">
        {/* Summary */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-6 mb-8">
          <div className="bg-white rounded-xl p-6 shadow-sm border border-gray-100">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-600 mb-1">Tổng vay</p>
                <p className="text-2xl font-bold text-gray-900">{formatMoney(summary.totalBorrowed)}</p>
              </div>
              <div className="w-12 h-12 bg-orange-100 rounded-lg flex items-center justify-center">
                <CreditCard className="w-6 h-6 text-orange-600" />
              </div>
            </div>
          </div>

          <div className="bg-white rounded-xl p-6 shadow-sm border border-gray-100">
            <p className="text-sm text-gray-600 mb-1">Đã trả</p>
            <p className="text-2xl font-bold text-green-600">{formatMoney(summary.totalPaid)}</p>
          </div>

          <div className="bg-white rounded-xl p-6 shadow-sm border border-gray-100">
            <p className="text-sm text-gray-600 mb-1">Còn nợ</p>
            <p className="text-2xl font-bold text-red-600">{formatMoney(summary.totalRemaining)}</p>
          </div>

          <div className="bg-white rounded-xl p-6 shadow-sm border border-gray-100">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-600 mb-1">Khoản vay đang trả</p>
                <p className="text-2xl font-bold text-gray-900">{summary.activeCount}</p>
              </div>
              <div className="w-12 h-12 bg-blue-100 rounded-lg flex items-center justify-center">
                <Calendar className="w-6 h-6 text-blue-600" />
              </div>
            </div>
          </div>
        </div>

        {/* Finance Companies Section */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-100 mb-8">
          <div className="p-6 border-b border-gray-100">
            <h2 className="text-lg font-semibold text-gray-900">Công ty tài chính uy tín</h2>
            <p className="text-sm text-gray-500 mt-1">Dịch vụ cho vay nhanh chóng, thủ tục đơn giản</p>
          </div>
          
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 p-6">
            {/* FE Credit */}
            <div className="p-4 border border-gray-200 rounded-lg hover:border-orange-500 hover:shadow-md transition-all">
              <div className="flex items-center gap-3 mb-3">
                <div className="w-12 h-12 bg-orange-500 rounded-lg flex items-center justify-center">
                  <CreditCard className="w-7 h-7 text-white" />
                </div>
                <div>
                  <h3 className="font-semibold text-gray-900">FE Credit</h3>
                  <p className="text-xs text-gray-500">Công ty Tài chính TNHH MTV</p>
                </div>
              </div>
              <div className="space-y-2 text-sm">
                <div className="flex justify-between">
                  <span className="text-gray-600">Vay tiêu dùng:</span>
                  <span className="font-semibold text-orange-600">1.5%/tháng</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-gray-600">Hạn mức:</span>
                  <span className="font-semibold">500 triệu VNĐ</span>
                </div>
              </div>
            </div>

            {/* Home Credit */}
            <div className="p-4 border border-gray-200 rounded-lg hover:border-blue-500 hover:shadow-md transition-all">
              <div className="flex items-center gap-3 mb-3">
                <div className="w-12 h-12 bg-blue-500 rounded-lg flex items-center justify-center">
                  <Building2 className="w-7 h-7 text-white" />
                </div>
                <div>
                  <h3 className="font-semibold text-gray-900">Home Credit</h3>
                  <p className="text-xs text-gray-500">Công ty Tài chính Trách nhiệm hữu hạn</p>
                </div>
              </div>
              <div className="space-y-2 text-sm">
                <div className="flex justify-between">
                  <span className="text-gray-600">Vay tiêu dùng:</span>
                  <span className="font-semibold text-blue-600">1.3%/tháng</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-gray-600">Hạn mức:</span>
                  <span className="font-semibold">300 triệu VNĐ</span>
                </div>
              </div>
            </div>

            {/* HD SAISON */}
            <div className="p-4 border border-gray-200 rounded-lg hover:border-purple-500 hover:shadow-md transition-all">
              <div className="flex items-center gap-3 mb-3">
                <div className="w-12 h-12 bg-purple-500 rounded-lg flex items-center justify-center">
                  <Calculator className="w-7 h-7 text-white" />
                </div>
                <div>
                  <h3 className="font-semibold text-gray-900">HD SAISON</h3>
                  <p className="text-xs text-gray-500">Công ty Tài chính HD SAISON</p>
                </div>
              </div>
              <div className="space-y-2 text-sm">
                <div className="flex justify-between">
                  <span className="text-gray-600">Vay tiêu dùng:</span>
                  <span className="font-semibold text-purple-600">1.4%/tháng</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-gray-600">Hạn mức:</span>
                  <span className="font-semibold">400 triệu VNĐ</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Loans List */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-100">
          <div className="p-6 border-b border-gray-100">
            <h2 className="text-lg font-semibold text-gray-900">Danh sách khoản vay</h2>
          </div>
          
          <div className="divide-y divide-gray-100">
            {loans.length === 0 ? (
              <div className="p-12 text-center text-gray-500">
                <CreditCard className="w-16 h-16 mx-auto mb-4 text-gray-300" />
                <p className="text-lg font-medium mb-2">Chưa có khoản vay</p>
                <p className="text-sm">Vay tiền khi cần thiết với lãi suất hợp lý</p>
              </div>
            ) : (
              loans.map((loan) => (
                <div key={loan.id} className="p-6 hover:bg-gray-50 transition-colors">
                  <div className="flex items-center justify-between mb-4">
                    <div className="flex items-center gap-3">
                      <h3 className="text-lg font-semibold text-gray-900">{loan.type}</h3>
                      <span className={`px-3 py-1 rounded-full text-xs font-medium ${
                        loan.status === 'active' ? 'bg-orange-100 text-orange-700' :
                        loan.status === 'paid' ? 'bg-green-100 text-green-700' :
                        'bg-red-100 text-red-700'
                      }`}>
                        {loan.status === 'active' ? 'Đang trả' : loan.status === 'paid' ? 'Đã trả hết' : 'Quá hạn'}
                      </span>
                    </div>
                  </div>

                  {/* Progress Bar */}
                  <div className="mb-4">
                    <div className="flex items-center justify-between text-sm mb-2">
                      <span className="text-gray-600">Tiến độ thanh toán</span>
                      <span className="font-semibold text-gray-900">{loan.paidPercent.toFixed(1)}%</span>
                    </div>
                    <div className="w-full bg-gray-200 rounded-full h-2">
                      <div
                        className="bg-green-600 h-2 rounded-full transition-all"
                        style={{ width: `${loan.paidPercent}%` }}
                      ></div>
                    </div>
                  </div>
                  
                  <div className="grid grid-cols-2 md:grid-cols-5 gap-4 text-sm">
                    <div>
                      <p className="text-gray-500">Số tiền vay</p>
                      <p className="font-semibold text-gray-900">{formatMoney(loan.amount)}</p>
                    </div>
                    <div>
                      <p className="text-gray-500">Còn nợ</p>
                      <p className="font-semibold text-red-600">{formatMoney(loan.remainingAmount)}</p>
                    </div>
                    <div>
                      <p className="text-gray-500">Lãi suất</p>
                      <p className="font-semibold text-orange-600">{loan.interestRate}%/năm</p>
                    </div>
                    <div>
                      <p className="text-gray-500">Trả hàng tháng</p>
                      <p className="font-semibold text-gray-900">{formatMoney(loan.monthlyPayment)}</p>
                    </div>
                    <div>
                      <p className="text-gray-500">Kỳ thanh toán tiếp</p>
                      <p className="font-semibold text-gray-900">
                        {new Date(loan.nextPaymentDate).toLocaleDateString('vi-VN')}
                      </p>
                    </div>
                  </div>

                  <div className="mt-3 text-xs text-gray-500">
                    Ngày vay: {new Date(loan.startDate).toLocaleDateString('vi-VN')} • Kỳ hạn: {loan.term} tháng
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      </div>

      {/* Create Modal */}
      <Dialog open={showCreateModal} onOpenChange={setShowCreateModal}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Tạo khoản vay mới</DialogTitle>
            <DialogDescription>
              Vay tiền với lãi suất cạnh tranh
            </DialogDescription>
          </DialogHeader>
          
          <div className="space-y-4 py-4">
            <div>
              <Label htmlFor="type">Loại vay</Label>
              <select
                id="type"
                value={formData.type}
                onChange={(e) => setFormData({ ...formData, type: e.target.value, term: '12' })}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-orange-500"
              >
                {(policy?.loans.map((p) => p.type) ?? DEFAULT_LOAN_TYPES).map((type) => (
                  <option key={type} value={type}>{type}</option>
                ))}
              </select>
            </div>

            <div>
              <Label htmlFor="amount">Số tiền vay (VND)</Label>
              <Input
                id="amount"
                type="number"
                value={formData.amount}
                onChange={(e) => setFormData({ ...formData, amount: e.target.value })}
                placeholder={product ? `Từ ${product.minAmount.toLocaleString('vi-VN')} đến ${product.maxAmount.toLocaleString('vi-VN')}` : 'Tối thiểu 10.000.000'}
              />
            </div>

            <div>
              <Label>Lãi suất (%/năm)</Label>
              <p className="px-3 py-2 border border-gray-200 bg-gray-50 rounded-lg text-gray-800">
                {product ? `${product.annualRate}%/năm theo sản phẩm ${product.type}` : policyError || 'Đang tải biểu lãi suất...'}
              </p>
            </div>

            <div>
              <Label htmlFor="term">Kỳ hạn (tháng)</Label>
              <select
                id="term"
                value={formData.term}
                onChange={(e) => setFormData({ ...formData, term: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-orange-500"
              >
                {termOptions.map((t) => (
                  <option key={t} value={t}>{t} tháng</option>
                ))}
              </select>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setShowCreateModal(false)}>Hủy</Button>
            <Button onClick={handleCreate} className="bg-orange-600 hover:bg-orange-700">Tạo khoản vay</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      {flow.dialog}
    </div>
  );
}

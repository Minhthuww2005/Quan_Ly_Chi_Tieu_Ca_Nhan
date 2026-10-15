import { useState, useEffect } from 'react';
import { ArrowLeft, Plus, TrendingUp, Calendar, DollarSign, Building2 } from 'lucide-react';
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

interface SavingsAccount {
  id: string;
  name: string;
  balance: number;
  interestRate: number;
  term: number;
  startDate: string;
  maturityDate: string;
  status: string;
  expectedInterest: number;
}

export default function SavingsPage() {
  const navigate = useNavigate();
  const [accounts, setAccounts] = useState<SavingsAccount[]>([]);
  const [summary, setSummary] = useState({ totalBalance: 0, totalInterest: 0, activeCount: 0 });
  const [loading, setLoading] = useState(true);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [formData, setFormData] = useState({
    name: '',
    amount: '',
    interestRate: '6.5',
    term: '12'
  });

  useEffect(() => {
    fetchSavings();
  }, []);

  const fetchSavings = async () => {
    try {
      const userInfo = JSON.parse(localStorage.getItem('userInfo') || '{}');
      const response = await fetch(`/api/savings/${userInfo.user_id}`);
      const data = await response.json();
      
      if (data.success) {
        setAccounts(data.accounts);
        setSummary(data.summary);
      }
    } catch (error) {
      console.error('Error fetching savings:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = async () => {
    try {
      const userInfo = JSON.parse(localStorage.getItem('userInfo') || '{}');
      const response = await fetch('/api/savings/create', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          user_id: userInfo.user_id,
          name: formData.name,
          amount: parseFloat(formData.amount),
          interestRate: parseFloat(formData.interestRate),
          term: parseInt(formData.term)
        })
      });

      const data = await response.json();
      if (data.success) {
        setShowCreateModal(false);
        fetchSavings();
        setFormData({ name: '', amount: '', interestRate: '6.5', term: '12' });
      } else {
        alert(data.message);
      }
    } catch (error) {
      console.error('Error creating savings:', error);
    }
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
      <div className="bg-gradient-to-r from-blue-600 to-indigo-700 text-white">
        <div className="max-w-7xl mx-auto px-4 py-6">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-4">
              <button onClick={() => navigate(-1)} className="p-2 hover:bg-white/10 rounded-lg">
                <ArrowLeft className="w-6 h-6" />
              </button>
              <div>
                <h1 className="text-2xl font-bold">Sổ tiết kiệm</h1>
                <p className="text-blue-100 text-sm mt-1">Quản lý tài khoản tiết kiệm</p>
              </div>
            </div>
            <Button onClick={() => setShowCreateModal(true)} className="bg-white text-blue-600 hover:bg-blue-50">
              <Plus className="w-5 h-5 mr-2" />
              Mở sổ mới
            </Button>
          </div>
        </div>
      </div>

      <div className="max-w-7xl mx-auto px-4 py-8">
        {/* Summary Cards */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
          <div className="bg-white rounded-xl p-6 shadow-sm border border-gray-100">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-600 mb-1">Tổng tiền gửi</p>
                <p className="text-2xl font-bold text-gray-900">{formatMoney(summary.totalBalance)}</p>
              </div>
              <div className="w-12 h-12 bg-blue-100 rounded-lg flex items-center justify-center">
                <DollarSign className="w-6 h-6 text-blue-600" />
              </div>
            </div>
          </div>

          <div className="bg-white rounded-xl p-6 shadow-sm border border-gray-100">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-600 mb-1">Lãi dự kiến</p>
                <p className="text-2xl font-bold text-green-600">{formatMoney(summary.totalInterest)}</p>
              </div>
              <div className="w-12 h-12 bg-green-100 rounded-lg flex items-center justify-center">
                <TrendingUp className="w-6 h-6 text-green-600" />
              </div>
            </div>
          </div>

          <div className="bg-white rounded-xl p-6 shadow-sm border border-gray-100">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-600 mb-1">Sổ đang hoạt động</p>
                <p className="text-2xl font-bold text-gray-900">{summary.activeCount}</p>
              </div>
              <div className="w-12 h-12 bg-purple-100 rounded-lg flex items-center justify-center">
                <Calendar className="w-6 h-6 text-purple-600" />
              </div>
            </div>
          </div>
        </div>

        {/* Partner Banks Section */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-100 mb-8">
          <div className="p-6 border-b border-gray-100">
            <h2 className="text-lg font-semibold text-gray-900">Ngân hàng hợp tác</h2>
            <p className="text-sm text-gray-500 mt-1">Lãi suất tiết kiệm cạnh tranh từ các ngân hàng uy tín</p>
          </div>
          
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 p-6">
            {/* Vietcombank */}
            <div className="p-4 border border-gray-200 rounded-lg hover:border-green-500 hover:shadow-md transition-all">
              <div className="flex items-center gap-3 mb-3">
                <div className="w-12 h-12 bg-green-500 rounded-lg flex items-center justify-center">
                  <Building2 className="w-7 h-7 text-white" />
                </div>
                <div>
                  <h3 className="font-semibold text-gray-900">Vietcombank</h3>
                  <p className="text-xs text-gray-500">Ngân hàng TMCP Ngoại thương VN</p>
                </div>
              </div>
              <div className="space-y-2 text-sm">
                <div className="flex justify-between">
                  <span className="text-gray-600">6 tháng:</span>
                  <span className="font-semibold text-green-600">4.7%/năm</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-gray-600">12 tháng:</span>
                  <span className="font-semibold text-green-600">5.0%/năm</span>
                </div>
              </div>
            </div>

            {/* Vietinbank */}
            <div className="p-4 border border-gray-200 rounded-lg hover:border-blue-500 hover:shadow-md transition-all">
              <div className="flex items-center gap-3 mb-3">
                <div className="w-12 h-12 bg-blue-500 rounded-lg flex items-center justify-center">
                  <Building2 className="w-7 h-7 text-white" />
                </div>
                <div>
                  <h3 className="font-semibold text-gray-900">Vietinbank</h3>
                  <p className="text-xs text-gray-500">Ngân hàng TMCP Công Thương VN</p>
                </div>
              </div>
              <div className="space-y-2 text-sm">
                <div className="flex justify-between">
                  <span className="text-gray-600">6 tháng:</span>
                  <span className="font-semibold text-blue-600">4.8%/năm</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-gray-600">12 tháng:</span>
                  <span className="font-semibold text-blue-600">5.2%/năm</span>
                </div>
              </div>
            </div>

            {/* ACB */}
            <div className="p-4 border border-gray-200 rounded-lg hover:border-purple-500 hover:shadow-md transition-all">
              <div className="flex items-center gap-3 mb-3">
                <div className="w-12 h-12 bg-purple-500 rounded-lg flex items-center justify-center">
                  <Building2 className="w-7 h-7 text-white" />
                </div>
                <div>
                  <h3 className="font-semibold text-gray-900">ACB</h3>
                  <p className="text-xs text-gray-500">Ngân hàng Á Châu</p>
                </div>
              </div>
              <div className="space-y-2 text-sm">
                <div className="flex justify-between">
                  <span className="text-gray-600">6 tháng:</span>
                  <span className="font-semibold text-purple-600">5.5%/năm</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-gray-600">12 tháng:</span>
                  <span className="font-semibold text-purple-600">6.0%/năm</span>
                </div>
              </div>
            </div>

            {/* Techcombank */}
            <div className="p-4 border border-gray-200 rounded-lg hover:border-red-500 hover:shadow-md transition-all">
              <div className="flex items-center gap-3 mb-3">
                <div className="w-12 h-12 bg-red-500 rounded-lg flex items-center justify-center">
                  <Building2 className="w-7 h-7 text-white" />
                </div>
                <div>
                  <h3 className="font-semibold text-gray-900">Techcombank</h3>
                  <p className="text-xs text-gray-500">Ngân hàng Kỹ Thương VN</p>
                </div>
              </div>
              <div className="space-y-2 text-sm">
                <div className="flex justify-between">
                  <span className="text-gray-600">6 tháng:</span>
                  <span className="font-semibold text-red-600">5.3%/năm</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-gray-600">12 tháng:</span>
                  <span className="font-semibold text-red-600">5.8%/năm</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Accounts List */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-100">
          <div className="p-6 border-b border-gray-100">
            <h2 className="text-lg font-semibold text-gray-900">Danh sách sổ tiết kiệm</h2>
          </div>
          
          <div className="divide-y divide-gray-100">
            {accounts.length === 0 ? (
              <div className="p-12 text-center text-gray-500">
                <Calendar className="w-16 h-16 mx-auto mb-4 text-gray-300" />
                <p className="text-lg font-medium mb-2">Chưa có sổ tiết kiệm</p>
                <p className="text-sm">Mở sổ tiết kiệm để nhận lãi suất hấp dẫn</p>
              </div>
            ) : (
              accounts.map((account) => (
                <div key={account.id} className="p-6 hover:bg-gray-50 transition-colors">
                  <div className="flex items-center justify-between">
                    <div className="flex-1">
                      <div className="flex items-center gap-3 mb-2">
                        <h3 className="text-lg font-semibold text-gray-900">{account.name}</h3>
                        <span className={`px-3 py-1 rounded-full text-xs font-medium ${
                          account.status === 'active' ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-700'
                        }`}>
                          {account.status === 'active' ? 'Đang hoạt động' : account.status === 'matured' ? 'Đến hạn' : 'Đã đóng'}
                        </span>
                      </div>
                      
                      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
                        <div>
                          <p className="text-gray-500">Số tiền</p>
                          <p className="font-semibold text-gray-900">{formatMoney(account.balance)}</p>
                        </div>
                        <div>
                          <p className="text-gray-500">Lãi suất</p>
                          <p className="font-semibold text-blue-600">{account.interestRate}%/năm</p>
                        </div>
                        <div>
                          <p className="text-gray-500">Kỳ hạn</p>
                          <p className="font-semibold text-gray-900">{account.term} tháng</p>
                        </div>
                        <div>
                          <p className="text-gray-500">Lãi dự kiến</p>
                          <p className="font-semibold text-green-600">{formatMoney(account.expectedInterest)}</p>
                        </div>
                      </div>

                      <div className="mt-3 flex items-center gap-4 text-xs text-gray-500">
                        <span>Ngày gửi: {new Date(account.startDate).toLocaleDateString('vi-VN')}</span>
                        <span>•</span>
                        <span>Ngày đến hạn: {new Date(account.maturityDate).toLocaleDateString('vi-VN')}</span>
                      </div>
                    </div>
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
            <DialogTitle>Mở sổ tiết kiệm mới</DialogTitle>
            <DialogDescription>
              Gửi tiền tiết kiệm để nhận lãi suất hấp dẫn
            </DialogDescription>
          </DialogHeader>
          
          <div className="space-y-4 py-4">
            <div>
              <Label htmlFor="name">Tên sổ tiết kiệm</Label>
              <Input
                id="name"
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                placeholder="VD: Tiết kiệm mua nhà"
              />
            </div>

            <div>
              <Label htmlFor="amount">Số tiền gửi (VND)</Label>
              <Input
                id="amount"
                type="number"
                value={formData.amount}
                onChange={(e) => setFormData({ ...formData, amount: e.target.value })}
                placeholder="Tối thiểu 1.000.000"
              />
            </div>

            <div>
              <Label htmlFor="rate">Lãi suất (%/năm)</Label>
              <select
                id="rate"
                value={formData.interestRate}
                onChange={(e) => setFormData({ ...formData, interestRate: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              >
                <option value="5.5">5.5% - 1-3 tháng</option>
                <option value="6.0">6.0% - 3-6 tháng</option>
                <option value="6.5">6.5% - 6-12 tháng</option>
                <option value="7.0">7.0% - 12-24 tháng</option>
              </select>
            </div>

            <div>
              <Label htmlFor="term">Kỳ hạn (tháng)</Label>
              <select
                id="term"
                value={formData.term}
                onChange={(e) => setFormData({ ...formData, term: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              >
                <option value="1">1 tháng</option>
                <option value="3">3 tháng</option>
                <option value="6">6 tháng</option>
                <option value="12">12 tháng</option>
                <option value="24">24 tháng</option>
              </select>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setShowCreateModal(false)}>Hủy</Button>
            <Button onClick={handleCreate}>Mở sổ</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

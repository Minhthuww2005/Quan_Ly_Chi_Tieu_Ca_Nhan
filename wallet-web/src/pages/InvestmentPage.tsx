import { useState, useEffect } from 'react';
import { ArrowLeft, Plus, TrendingUp, TrendingDown, ExternalLink, RefreshCw, BarChart3 } from 'lucide-react';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../components/ui/select';

interface Investment {
  id: string;
  type: string;
  name: string;
  amount: number;
  quantity?: number;
  purchasePrice?: number;
  currentValue: number;
  profit: number;
  profitPercent: number;
  purchaseDate: string;
  status: string;
}

export default function InvestmentPage() {
  const navigate = useNavigate();
  const [investments, setInvestments] = useState<Investment[]>([]);
  const [summary, setSummary] = useState({ totalInvested: 0, totalValue: 0, totalProfit: 0, overallReturn: 0 });
  const [loading, setLoading] = useState(true);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [syncingPrices, setSyncingPrices] = useState<string[]>([]);
  const [formData, setFormData] = useState({
    type: 'Chứng khoán',
    name: '',
    amount: ''
  });

  useEffect(() => {
    fetchInvestments();
  }, []);

  const fetchInvestments = async () => {
    try {
      const userInfo = JSON.parse(localStorage.getItem('userInfo') || '{}');
      const response = await fetch(`/api/investments/${userInfo.user_id}`);
      const data = await response.json();
      
      if (data.success) {
        setInvestments(data.investments);
        setSummary(data.summary);
      }
    } catch (error) {
      console.error('Error fetching investments:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleSyncPrice = async (investmentId: string) => {
    try {
      setSyncingPrices([...syncingPrices, investmentId]);
      const response = await fetch('/api/investments/sync-price', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ investment_id: investmentId })
      });

      const data = await response.json();
      if (data.success) {
        fetchInvestments(); // Refresh data
        const details = [
          `✅ Cập nhật giá thành công!`,
          ``,
          `💰 Số lượng: ${data.quantity?.toFixed(8) || 'N/A'}`,
          `📊 Giá mua: ${data.purchasePrice?.toLocaleString('vi-VN') || 'N/A'} VND`,
          `📈 Giá hiện tại: ${data.currentPrice?.toLocaleString('vi-VN') || 'N/A'} VND`,
          `💵 Giá USD: $${data.priceUSD?.toLocaleString('en-US') || 'N/A'}`,
          ``,
          `💸 Vốn đầu tư: ${data.investmentAmount?.toLocaleString('vi-VN') || 'N/A'} VND`,
          `💎 Giá trị hiện tại: ${data.currentValue?.toLocaleString('vi-VN') || 'N/A'} VND`,
          `${data.profit >= 0 ? '📈' : '📉'} Lãi/Lỗ: ${data.profit >= 0 ? '+' : ''}${data.profitPercent?.toFixed(2) || '0'}%`
        ].join('\n');
        alert(details);
      } else {
        alert(data.message);
      }
    } catch (error) {
      console.error('Error syncing price:', error);
      alert('Lỗi khi đồng bộ giá');
    } finally {
      setSyncingPrices(syncingPrices.filter(id => id !== investmentId));
    }
  };

  const handleCreate = async () => {
    try {
      const userInfo = JSON.parse(localStorage.getItem('userInfo') || '{}');
      const response = await fetch('/api/investments/create', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          user_id: userInfo.user_id,
          type: formData.type,
          name: formData.name,
          amount: parseFloat(formData.amount)
        })
      });

      const data = await response.json();
      if (data.success) {
        setShowCreateModal(false);
        fetchInvestments();
        setFormData({ type: 'Chứng khoán', name: '', amount: '' });
      } else {
        alert(data.message);
      }
    } catch (error) {
      console.error('Error creating investment:', error);
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
      <div className="bg-gradient-to-r from-purple-600 to-pink-600 text-white">
        <div className="max-w-7xl mx-auto px-4 py-6">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-4">
              <button onClick={() => navigate(-1)} className="p-2 hover:bg-white/10 rounded-lg">
                <ArrowLeft className="w-6 h-6" />
              </button>
              <div>
                <h1 className="text-2xl font-bold">Đầu tư</h1>
                <p className="text-purple-100 text-sm mt-1">Quản lý danh mục đầu tư</p>
              </div>
            </div>
            <Button onClick={() => setShowCreateModal(true)} className="bg-white text-purple-600 hover:bg-purple-50">
              <Plus className="w-5 h-5 mr-2" />
              Tạo khoản đầu tư
            </Button>
          </div>
        </div>
      </div>

      <div className="max-w-7xl mx-auto px-4 py-8">
        {/* Summary */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-6 mb-8">
          <div className="bg-white rounded-xl p-6 shadow-sm border border-gray-100">
            <p className="text-sm text-gray-600 mb-1">Tổng vốn đầu tư</p>
            <p className="text-2xl font-bold text-gray-900">{formatMoney(summary.totalInvested)}</p>
          </div>

          <div className="bg-white rounded-xl p-6 shadow-sm border border-gray-100">
            <p className="text-sm text-gray-600 mb-1">Giá trị hiện tại</p>
            <p className="text-2xl font-bold text-gray-900">{formatMoney(summary.totalValue)}</p>
          </div>

          <div className="bg-white rounded-xl p-6 shadow-sm border border-gray-100">
            <p className="text-sm text-gray-600 mb-1">Lãi/Lỗ</p>
            <p className={`text-2xl font-bold ${summary.totalProfit >= 0 ? 'text-green-600' : 'text-red-600'}`}>
              {formatMoney(summary.totalProfit)}
            </p>
          </div>

          <div className="bg-white rounded-xl p-6 shadow-sm border border-gray-100">
            <p className="text-sm text-gray-600 mb-1">Tỷ suất lợi nhuận</p>
            <p className={`text-2xl font-bold ${summary.overallReturn >= 0 ? 'text-green-600' : 'text-red-600'}`}>
              {summary.overallReturn.toFixed(2)}%
            </p>
          </div>
        </div>

        {/* Exchange Links Section */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-100 mb-8">
          <div className="p-6 border-b border-gray-100">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-semibold text-gray-900">Sàn giao dịch</h2>
              <Button variant="outline" size="sm" onClick={fetchInvestments}>
                <RefreshCw className="w-4 h-4 mr-2" />
                Làm mới
              </Button>
            </div>
            <p className="text-sm text-gray-500 mt-1">Theo dõi giá và thị trường trên các sàn uy tín</p>
          </div>
          
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 p-6">
            {/* Binance */}
            <a 
              href="https://www.binance.com/vi/markets/overview" 
              target="_blank" 
              rel="noopener noreferrer"
              className="group p-4 border border-gray-200 rounded-lg hover:border-yellow-500 hover:shadow-md transition-all"
            >
              <div className="flex items-center justify-between mb-3">
                <div className="flex items-center gap-2">
                  <div className="w-10 h-10 bg-yellow-500 rounded-lg flex items-center justify-center">
                    <BarChart3 className="w-6 h-6 text-white" />
                  </div>
                  <div>
                    <h3 className="font-semibold text-gray-900">Binance</h3>
                    <p className="text-xs text-gray-500">Crypto Exchange</p>
                  </div>
                </div>
                <ExternalLink className="w-4 h-4 text-gray-400 group-hover:text-yellow-500" />
              </div>
              <p className="text-sm text-gray-600">BTC, ETH, BNB và 350+ coins</p>
            </a>

            {/* Huobi */}
            <a 
              href="https://www.huobi.com/en-us/markets/" 
              target="_blank" 
              rel="noopener noreferrer"
              className="group p-4 border border-gray-200 rounded-lg hover:border-blue-500 hover:shadow-md transition-all"
            >
              <div className="flex items-center justify-between mb-3">
                <div className="flex items-center gap-2">
                  <div className="w-10 h-10 bg-blue-500 rounded-lg flex items-center justify-center">
                    <BarChart3 className="w-6 h-6 text-white" />
                  </div>
                  <div>
                    <h3 className="font-semibold text-gray-900">Huobi</h3>
                    <p className="text-xs text-gray-500">Crypto Exchange</p>
                  </div>
                </div>
                <ExternalLink className="w-4 h-4 text-gray-400 group-hover:text-blue-500" />
              </div>
              <p className="text-sm text-gray-600">HT, BTC, ETH và 200+ coins</p>
            </a>

            {/* Coinbase */}
            <a 
              href="https://www.coinbase.com/price" 
              target="_blank" 
              rel="noopener noreferrer"
              className="group p-4 border border-gray-200 rounded-lg hover:border-blue-600 hover:shadow-md transition-all"
            >
              <div className="flex items-center justify-between mb-3">
                <div className="flex items-center gap-2">
                  <div className="w-10 h-10 bg-blue-600 rounded-lg flex items-center justify-center">
                    <BarChart3 className="w-6 h-6 text-white" />
                  </div>
                  <div>
                    <h3 className="font-semibold text-gray-900">Coinbase</h3>
                    <p className="text-xs text-gray-500">Crypto Exchange</p>
                  </div>
                </div>
                <ExternalLink className="w-4 h-4 text-gray-400 group-hover:text-blue-600" />
              </div>
              <p className="text-sm text-gray-600">BTC, ETH, USD và nhiều hơn</p>
            </a>

            {/* Trading View */}
            <a 
              href="https://www.tradingview.com/markets/cryptocurrencies/prices-all/" 
              target="_blank" 
              rel="noopener noreferrer"
              className="group p-4 border border-gray-200 rounded-lg hover:border-indigo-600 hover:shadow-md transition-all"
            >
              <div className="flex items-center justify-between mb-3">
                <div className="flex items-center gap-2">
                  <div className="w-10 h-10 bg-indigo-600 rounded-lg flex items-center justify-center">
                    <TrendingUp className="w-6 h-6 text-white" />
                  </div>
                  <div>
                    <h3 className="font-semibold text-gray-900">TradingView</h3>
                    <p className="text-xs text-gray-500">Chart Platform</p>
                  </div>
                </div>
                <ExternalLink className="w-4 h-4 text-gray-400 group-hover:text-indigo-600" />
              </div>
              <p className="text-sm text-gray-600">Biểu đồ & phân tích kỹ thuật</p>
            </a>
          </div>
        </div>

        {/* Investments List */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-100">
          <div className="p-6 border-b border-gray-100">
            <h2 className="text-lg font-semibold text-gray-900">Danh mục đầu tư</h2>
          </div>
          
          <div className="divide-y divide-gray-100">
            {investments.length === 0 ? (
              <div className="p-12 text-center text-gray-500">
                <TrendingUp className="w-16 h-16 mx-auto mb-4 text-gray-300" />
                <p className="text-lg font-medium mb-2">Chưa có khoản đầu tư</p>
                <p className="text-sm">Bắt đầu đầu tư để gia tăng tài sản</p>
              </div>
            ) : (
              investments.map((investment) => (
                <div key={investment.id} className="p-6 hover:bg-gray-50 transition-colors">
                  <div className="flex items-center justify-between">
                    <div className="flex-1">
                      <div className="flex items-center gap-3 mb-2">
                        <h3 className="text-lg font-semibold text-gray-900">{investment.name}</h3>
                        <span className="px-3 py-1 rounded-full text-xs font-medium bg-purple-100 text-purple-700">
                          {investment.type}
                        </span>
                        <span className={`px-3 py-1 rounded-full text-xs font-medium ${
                          investment.status === 'active' ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-700'
                        }`}>
                          {investment.status === 'active' ? 'Đang nắm giữ' : 'Đã bán'}
                        </span>
                      </div>
                      
                      <div className="grid grid-cols-2 md:grid-cols-5 gap-4 text-sm">
                        <div>
                          <p className="text-gray-500">Vốn đầu tư</p>
                          <p className="font-semibold text-gray-900">{formatMoney(investment.amount)}</p>
                        </div>
                        <div>
                          <p className="text-gray-500">Giá trị hiện tại</p>
                          <p className="font-semibold text-gray-900">{formatMoney(investment.currentValue)}</p>
                        </div>
                        <div>
                          <p className="text-gray-500">Lãi/Lỗ</p>
                          <div className="flex items-center gap-1">
                            {investment.profit >= 0 ? (
                              <TrendingUp className="w-4 h-4 text-green-600" />
                            ) : (
                              <TrendingDown className="w-4 h-4 text-red-600" />
                            )}
                            <p className={`font-semibold ${investment.profit >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                              {formatMoney(investment.profit)}
                            </p>
                          </div>
                        </div>
                        <div>
                          <p className="text-gray-500">Tỷ suất</p>
                          <p className={`font-semibold ${investment.profitPercent >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                            {investment.profitPercent.toFixed(2)}%
                          </p>
                        </div>
                        <div>
                          <p className="text-gray-500">Ngày mua</p>
                          <p className="font-semibold text-gray-900">
                            {new Date(investment.purchaseDate).toLocaleDateString('vi-VN')}
                          </p>
                        </div>
                      </div>
                    </div>
                    
                    {investment.type === 'Tiền điện tử' && investment.status === 'active' && (
                      <div className="ml-4">
                        <Button 
                          onClick={() => handleSyncPrice(investment.id)}
                          disabled={syncingPrices.includes(investment.id)}
                          size="sm"
                          variant="outline"
                          className="gap-2"
                        >
                          <RefreshCw className={`w-4 h-4 ${syncingPrices.includes(investment.id) ? 'animate-spin' : ''}`} />
                          {syncingPrices.includes(investment.id) ? 'Đang cập nhật...' : 'Cập nhật giá'}
                        </Button>
                      </div>
                    )}
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
            <DialogTitle>Tạo khoản đầu tư mới</DialogTitle>
            <DialogDescription>
              Thêm khoản đầu tư vào danh mục của bạn
            </DialogDescription>
          </DialogHeader>
          
          <div className="space-y-4 py-4">
            <div>
              <Label htmlFor="type">Loại đầu tư</Label>
              <Select value={formData.type} onValueChange={(value) => setFormData({ ...formData, type: value })}>
                <SelectTrigger className="w-full">
                  <SelectValue placeholder="Chọn loại đầu tư" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Chứng khoán">📈 Chứng khoán</SelectItem>
                  <SelectItem value="Quỹ đầu tư">💼 Quỹ đầu tư</SelectItem>
                  <SelectItem value="Bất động sản">🏠 Bất động sản</SelectItem>
                  <SelectItem value="Vàng">🪙 Vàng</SelectItem>
                  <SelectItem value="Tiền điện tử">₿ Tiền điện tử (Crypto)</SelectItem>
                  <SelectItem value="Trái phiếu">📄 Trái phiếu</SelectItem>
                  <SelectItem value="Khác">📊 Khác</SelectItem>
                </SelectContent>
              </Select>
              <p className="text-xs text-gray-500 mt-1">
                {formData.type === 'Tiền điện tử' && 'Truy cập Binance, Huobi để mua BTC, ETH...'}
                {formData.type === 'Chứng khoán' && 'Cổ phiếu niêm yết trên HOSE, HNX, UPCOM'}
                {formData.type === 'Vàng' && 'Vàng SJC, vàng miếng, vàng trang sức'}
              </p>
            </div>

            <div>
              <Label htmlFor="name">Tên khoản đầu tư</Label>
              <Input
                id="name"
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                placeholder={
                  formData.type === 'Tiền điện tử' ? 'VD: Bitcoin, Ethereum, BNB' :
                  formData.type === 'Chứng khoán' ? 'VD: VNM, VIC, HPG' :
                  formData.type === 'Vàng' ? 'VD: Vàng SJC 1 chỉ' :
                  'Nhập tên khoản đầu tư'
                }
              />
            </div>

            <div>
              <Label htmlFor="amount">Số tiền đầu tư (VND)</Label>
              <Input
                id="amount"
                type="number"
                value={formData.amount}
                onChange={(e) => setFormData({ ...formData, amount: e.target.value })}
                placeholder="Tối thiểu 5.000.000"
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setShowCreateModal(false)}>Hủy</Button>
            <Button onClick={handleCreate} className="bg-purple-600 hover:bg-purple-700">Tạo đầu tư</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

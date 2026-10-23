import { useState } from 'react';
import { PiggyBank, Banknote, Shield, TrendingUp, Calculator } from 'lucide-react';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../components/ui/select';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '../components/ui/dialog';
import { Card, CardContent,} from '../components/ui/card';
import { Badge } from '../components/ui/badge';
import { Slider } from '../components/ui/slider';
import { formatCurrency } from '../data/bankingData';
import { loanProduct, savingsRate, useFinancePolicy } from '../services/financePolicy';

interface ServiceModalProps {
  isOpen: boolean;
  onClose: () => void;
  type: 'savings' | 'loan' | 'insurance' | 'investment' | null;
}

export default function ServiceModal({ isOpen, onClose, type }: ServiceModalProps) {
  const [selectedProduct, setSelectedProduct] = useState('');
  const [amount, setAmount] = useState('');
  const [term, setTerm] = useState(12);
  const [loanAmount, setLoanAmount] = useState([50000000]);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');
  const { policy } = useFinancePolicy();

  const savingsProducts = [
    { id: '1', name: 'Tiết kiệm có kỳ hạn', minAmount: 1000000, terms: [6, 12, 18, 24, 36] },
    { id: '2', name: 'Tiết kiệm linh hoạt', minAmount: 500000, terms: [0] },
    { id: '3', name: 'Tiết kiệm tích lũy', minAmount: 100000, terms: [12, 24, 36] }
  ];

  const loanProducts = [
    { id: '1', name: 'Vay tiêu dùng', terms: [12, 24, 36, 48, 60] },
    { id: '2', name: 'Vay mua nhà', terms: [120, 180, 240, 300] },
    { id: '3', name: 'Vay mua xe', terms: [24, 36, 48, 60, 84] }
  ].map((p) => {
    // Rate, amount limit and maximum term come from the server so the estimate matches what is applied.
    const server = loanProduct(policy, p.name);
    return { ...p, rate: server?.annualRate ?? null, maxAmount: server?.maxAmount ?? 500000000, terms: p.terms.filter((t) => !server || t <= server.maxTermMonths) };
  });
  const savingsRateFor = (t: number) => savingsRate(policy, t);

  const insuranceProducts = [
    { id: '1', name: 'Bảo hiểm nhân thọ', premium: 2400000, coverage: 500000000, term: 10 },
    { id: '2', name: 'Bảo hiểm sức khỏe', premium: 1800000, coverage: 200000000, term: 1 },
    { id: '3', name: 'Bảo hiểm tai nạn', premium: 600000, coverage: 100000000, term: 1 }
  ];

  const investmentProducts = [
    { id: '1', name: 'Quỹ đầu tư cân bằng', expectedReturn: 8.5, risk: 'Thấp', minAmount: 10000000 },
    { id: '2', name: 'Quỹ đầu tư tăng trưởng', expectedReturn: 12.0, risk: 'Trung bình', minAmount: 5000000 },
    { id: '3', name: 'Quỹ đầu tư cổ phiếu', expectedReturn: 15.0, risk: 'Cao', minAmount: 20000000 }
  ];

  const calculateSavingsInterest = () => {
    const principal = parseInt(amount) || 0;
    const product = savingsProducts.find(p => p.id === selectedProduct);
    const rate = savingsRateFor(term);
    if (!product || rate === null) return 0;

    const monthlyRate = rate / 100 / 12;
    return principal * monthlyRate * term;
  };

  const calculateLoanPayment = () => {
    const principal = loanAmount[0];
    const product = loanProducts.find(p => p.id === selectedProduct);
    if (!product || product.rate === null) return 0;

    const monthlyRate = product.rate / 100 / 12;
    const payment = (principal * monthlyRate * Math.pow(1 + monthlyRate, term)) / 
                   (Math.pow(1 + monthlyRate, term) - 1);
    return payment;
  };

  const handleSubmit = async () => {
    setSubmitting(true);
    setError('');
    try {
      const user = JSON.parse(localStorage.getItem('userInfo') || '{}');
      if (!user.user_id) throw new Error('Vui lòng đăng nhập');
      const savings = savingsProducts.find(p => p.id === selectedProduct);
      const loan = loanProducts.find(p => p.id === selectedProduct);
      const insurance = insuranceProducts.find(p => p.id === selectedProduct);
      const investment = investmentProducts.find(p => p.id === selectedProduct);
      const details = type === 'savings'
        ? { name: savings!.name, amount: Number(amount), term }
        : type === 'loan'
        ? { type: loan!.name, amount: loanAmount[0], term }
        : type === 'insurance'
        ? { name: insurance!.name, type: insurance!.name, premium: insurance!.premium, coverage: insurance!.coverage, term: insurance!.term }
        : { name: investment!.name, type: 'Quỹ đầu tư', amount: Number(amount) };
      const area = type === 'loan' ? 'loans' : type === 'investment' ? 'investments' : type;
      const response = await fetch(`/api/${area}/create`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ user_id: user.user_id, ...details }),
      });
      const result = await response.json();
      if (!response.ok || !result.success) throw new Error(result.message || 'Không thể đăng ký');
      alert(`Đã tạo ${getServiceName()} thành công.`);
      onClose();
      window.location.reload();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không kết nối được máy chủ');
    } finally { setSubmitting(false); }
  };

  const getServiceName = () => {
    switch (type) {
      case 'savings': return 'sổ tiết kiệm';
      case 'loan': return 'khoản vay';
      case 'insurance': return 'bảo hiểm';
      case 'investment': return 'sản phẩm đầu tư';
      default: return '';
    }
  };

  const getIcon = () => {
    switch (type) {
      case 'savings': return <PiggyBank className="w-6 h-6" />;
      case 'loan': return <Banknote className="w-6 h-6" />;
      case 'insurance': return <Shield className="w-6 h-6" />;
      case 'investment': return <TrendingUp className="w-6 h-6" />;
      default: return null;
    }
  };

  const renderSavingsContent = () => (
    <div className="space-y-4">
      <div>
        <Label>Chọn sản phẩm tiết kiệm</Label>
        <Select value={selectedProduct} onValueChange={setSelectedProduct}>
          <SelectTrigger>
            <SelectValue placeholder="Chọn loại tiết kiệm" />
          </SelectTrigger>
          <SelectContent>
            {savingsProducts.map(product => (
              <SelectItem key={product.id} value={product.id}>
                {product.name} - {product.terms[0] === 0 ? `${savingsRateFor(0) ?? '...'}%/năm` : 'lãi suất theo kỳ hạn'}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {selectedProduct && (
        <>
          <div>
            <Label>Số tiền gửi</Label>
            <Input
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              placeholder="Nhập số tiền"
              type="number"
            />
          </div>

          <div>
            <Label>Kỳ hạn (tháng)</Label>
            <Select value={term.toString()} onValueChange={(value) => setTerm(parseInt(value))}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {savingsProducts.find(p => p.id === selectedProduct)?.terms.map(termOption => (
                  <SelectItem key={termOption} value={termOption.toString()}>
                    {termOption === 0 ? 'Không kỳ hạn' : `${termOption} tháng`}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-sm text-gray-600 mt-1">Lãi suất áp dụng: {savingsRateFor(term) ?? '...'}%/năm</p>
          </div>

          {amount && (
            <Card className="bg-green-50">
              <CardContent className="p-4">
                <div className="flex items-center space-x-2 mb-2">
                  <Calculator className="w-4 h-4 text-green-600" />
                  <span className="font-medium text-green-800">Dự tính lãi nhận được</span>
                </div>
                <div className="text-2xl font-bold text-green-700">
                  {formatCurrency(calculateSavingsInterest())}
                </div>
                <div className="text-sm text-green-600 mt-1">
                  Tổng tiền nhận được: {formatCurrency((parseInt(amount) || 0) + calculateSavingsInterest())}
                </div>
              </CardContent>
            </Card>
          )}
        </>
      )}
    </div>
  );

  const renderLoanContent = () => (
    <div className="space-y-4">
      <div>
        <Label>Chọn sản phẩm vay</Label>
        <Select value={selectedProduct} onValueChange={setSelectedProduct}>
          <SelectTrigger>
            <SelectValue placeholder="Chọn loại vay" />
          </SelectTrigger>
          <SelectContent>
            {loanProducts.map(product => (
              <SelectItem key={product.id} value={product.id}>
                {product.name} - {product.rate ?? '...'}%/năm
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {selectedProduct && (
        <>
          <div>
            <Label>Số tiền vay: {formatCurrency(loanAmount[0])}</Label>
            <Slider
              value={loanAmount}
              onValueChange={setLoanAmount}
              max={loanProducts.find(p => p.id === selectedProduct)?.maxAmount || 500000000}
              min={10000000}
              step={1000000}
              className="mt-2"
            />
          </div>

          <div>
            <Label>Thời hạn vay (tháng)</Label>
            <Select value={term.toString()} onValueChange={(value) => setTerm(parseInt(value))}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {loanProducts.find(p => p.id === selectedProduct)?.terms.map(termOption => (
                  <SelectItem key={termOption} value={termOption.toString()}>
                    {termOption} tháng ({Math.round(termOption / 12 * 10) / 10} năm)
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <Card className="bg-blue-50">
            <CardContent className="p-4">
              <div className="flex items-center space-x-2 mb-2">
                <Calculator className="w-4 h-4 text-blue-600" />
                <span className="font-medium text-blue-800">Khoản thanh toán hàng tháng</span>
              </div>
              <div className="text-2xl font-bold text-blue-700">
                {formatCurrency(calculateLoanPayment())}
              </div>
              <div className="text-sm text-blue-600 mt-1">
                Tổng tiền trả: {formatCurrency(calculateLoanPayment() * term)}
              </div>
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );

  const renderInsuranceContent = () => (
    <div className="space-y-4">
      <div className="grid gap-3">
        {insuranceProducts.map(product => (
          <Card key={product.id} className={`cursor-pointer transition-all ${
            selectedProduct === product.id ? 'ring-2 ring-gray-900' : 'hover:shadow-md'
          }`} onClick={() => setSelectedProduct(product.id)}>
            <CardContent className="p-4">
              <div className="flex justify-between items-start mb-2">
                <h3 className="font-medium text-gray-900">{product.name}</h3>
                <Badge variant="secondary">{product.term} năm</Badge>
              </div>
              <div className="space-y-1 text-sm text-gray-600">
                <div>Phí bảo hiểm: {formatCurrency(product.premium)}/năm</div>
                <div>Tổng phí trừ ví: {formatCurrency(product.premium * product.term)}</div>
                <div>Số tiền bảo hiểm: {formatCurrency(product.coverage)}</div>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );

  const renderInvestmentContent = () => (
    <div className="space-y-4">
      <div className="grid gap-3">
        {investmentProducts.map(product => (
          <Card key={product.id} className={`cursor-pointer transition-all ${
            selectedProduct === product.id ? 'ring-2 ring-gray-900' : 'hover:shadow-md'
          }`} onClick={() => setSelectedProduct(product.id)}>
            <CardContent className="p-4">
              <div className="flex justify-between items-start mb-2">
                <h3 className="font-medium text-gray-900">{product.name}</h3>
                <Badge variant={product.risk === 'Thấp' ? 'secondary' : product.risk === 'Trung bình' ? 'default' : 'destructive'}>
                  {product.risk}
                </Badge>
              </div>
              <div className="space-y-1 text-sm text-gray-600">
                <div>Lợi nhuận kỳ vọng: {product.expectedReturn}%/năm</div>
                <div>Số tiền đầu tư tối thiểu: {formatCurrency(product.minAmount)}</div>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      {selectedProduct && (
        <div>
          <Label>Số tiền đầu tư</Label>
          <Input
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            placeholder="Nhập số tiền đầu tư"
            type="number"
          />
        </div>
      )}
    </div>
  );

  const getTitle = () => {
    switch (type) {
      case 'savings': return 'Mở sổ tiết kiệm';
      case 'loan': return 'Đăng ký vay';
      case 'insurance': return 'Mua bảo hiểm';
      case 'investment': return 'Đầu tư chứng khoán';
      default: return '';
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-md max-h-[80vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center space-x-2">
            {getIcon()}
            <span>{getTitle()}</span>
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          {type === 'savings' && renderSavingsContent()}
          {type === 'loan' && renderLoanContent()}
          {type === 'insurance' && renderInsuranceContent()}
          {type === 'investment' && renderInvestmentContent()}
          {error && <p role="alert" className="text-sm text-red-600">{error}</p>}

          <div className="flex space-x-3 pt-4">
            <Button variant="outline" onClick={onClose} className="flex-1">
              Hủy
            </Button>
            <Button 
              onClick={handleSubmit} 
              className="flex-1 bg-gray-900 hover:bg-gray-800"
              disabled={!selectedProduct || submitting}
            >
              Đăng ký ngay
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

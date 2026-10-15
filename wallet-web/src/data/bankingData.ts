import type { User, Transaction, BankCard, SavingsAccount, LoanAccount, Insurance, Investment } from '../types';

export const currentUser: User = {
  id: '1',
  name: 'Nguyễn Văn A',
  avatar: 'NVA',
  phone: '0987654321',
  email: 'nguyenvana@email.com',
  accountNumber: '1234567890',
  balance: 24500000000
};

export const bankCards: BankCard[] = [
  {
    id: '1',
    type: 'debit',
    number: '1234 5678 9012 3456',
    holder: 'NGUYEN VAN A',
    expiry: '12/28',
    balance: 24500000000,
    status: 'active'
  },
  {
    id: '2',
    type: 'credit',
    number: '9876 5432 1098 7654',
    holder: 'NGUYEN VAN A',
    expiry: '06/27',
    balance: 15000000,
    limit: 50000000,
    status: 'active'
  }
];

export const savingsAccounts: SavingsAccount[] = [
  {
    id: '1',
    name: 'Tiết kiệm có kỳ hạn 12 tháng',
    balance: 100000000,
    interestRate: 6.5,
    term: 12,
    startDate: '2024-01-15',
    maturityDate: '2025-01-15',
    status: 'active'
  },
  {
    id: '2',
    name: 'Tiết kiệm linh hoạt',
    balance: 50000000,
    interestRate: 4.2,
    term: 0,
    startDate: '2023-12-01',
    maturityDate: '',
    status: 'active'
  }
];

export const loanAccounts: LoanAccount[] = [
  {
    id: '1',
    type: 'Vay tiêu dùng',
    amount: 200000000,
    remainingAmount: 150000000,
    interestRate: 12.5,
    term: 36,
    monthlyPayment: 6500000,
    startDate: '2023-06-15',
    nextPaymentDate: '2024-02-15',
    status: 'active'
  }
];

export const insurances: Insurance[] = [
  {
    id: '1',
    type: 'Bảo hiểm nhân thọ',
    name: 'An Phát Thịnh Vượng',
    premium: 2400000,
    coverage: 500000000,
    startDate: '2024-01-01',
    endDate: '2034-01-01',
    status: 'active'
  },
  {
    id: '2',
    type: 'Bảo hiểm sức khỏe',
    name: 'Sức Khỏe Vàng',
    premium: 1800000,
    coverage: 200000000,
    startDate: '2024-01-01',
    endDate: '2025-01-01',
    status: 'active'
  }
];

export const investments: Investment[] = [
  {
    id: '1',
    type: 'Cổ phiếu',
    name: 'VCB - Vietcombank',
    amount: 50000000,
    currentValue: 55000000,
    profit: 5000000,
    profitPercent: 10.0,
    purchaseDate: '2023-12-01',
    status: 'active'
  },
  {
    id: '2',
    type: 'Trái phiếu',
    name: 'Trái phiếu Chính phủ 5 năm',
    amount: 100000000,
    currentValue: 102500000,
    profit: 2500000,
    profitPercent: 2.5,
    purchaseDate: '2024-01-01',
    status: 'active'
  }
];

export const allTransactions: Transaction[] = [
  {
    id: '1',
    type: 'deposit',
    title: 'Chuyển tiền từ VCB',
    description: 'Từ tài khoản VCB ****1234 • 21:30 15-01-2024',
    amount: 5000000,
    date: '2024-01-15T21:30:00Z',
    status: 'success',
    icon: '',
    color: 'bg-green-600'
  },
  {
    id: '2',
    type: 'payment',
    title: 'Thanh toán tiền điện',
    description: 'EVN HCMC • Mã hóa đơn: PD240115001 • 17:15 15-01-2024',
    amount: -850000,
    date: '2024-01-15T17:15:00Z',
    status: 'success',
    icon: '',
    color: 'bg-red-600'
  },
  {
    id: '3',
    type: 'transfer',
    title: 'Chuyển khoản đến TRẦN VĂN B',
    description: 'Techcombank ****5678 • Tiền ăn trưa • 23:45 14-01-2024',
    amount: -500000,
    date: '2024-01-14T23:45:00Z',
    status: 'success',
    icon: '',
    color: 'bg-red-600',
    recipient: 'TRẦN VĂN B',
    reference: 'Tiền ăn trưa'
  },
  {
    id: '4',
    type: 'payment',
    title: 'Nạp tiền điện thoại',
    description: 'Viettel 0987654321 • Gói cước 12 tháng • 12:25 14-01-2024',
    amount: -300000,
    date: '2024-01-14T12:25:00Z',
    status: 'success',
    icon: '',
    color: 'bg-red-600'
  },
  {
    id: '5',
    type: 'deposit',
    title: 'Lãi tiết kiệm tháng 1',
    description: 'Sổ tiết kiệm số 1234567890 • Lãi suất 6.5%/năm',
    amount: 125000,
    date: '2024-01-01T00:00:00Z',
    status: 'success',
    icon: '',
    color: 'bg-green-600'
  },
  {
    id: '6',
    type: 'loan',
    title: 'Giải ngân khoản vay',
    description: 'Vay tiêu dùng HTD • Số tiền: 200,000,000 VNĐ',
    amount: 200000000,
    date: '2023-06-15T10:00:00Z',
    status: 'success',
    icon: '',
    color: 'bg-green-600'
  },
  {
    id: '7',
    type: 'investment',
    title: 'Mua cổ phiếu VCB',
    description: 'Mua 1000 cổ phiếu VCB @ 50,000 VNĐ/cp',
    amount: -50000000,
    date: '2023-12-01T14:30:00Z',
    status: 'success',
    icon: '',
    color: 'bg-blue-600'
  },
  {
    id: '8',
    type: 'insurance',
    title: 'Đóng phí bảo hiểm',
    description: 'Bảo hiểm nhân thọ An Phát Thịnh Vượng • Phí năm 2024',
    amount: -2400000,
    date: '2024-01-01T09:00:00Z',
    status: 'success',
    icon: '',
    color: 'bg-purple-600'
  }
];

// Utility functions
export const formatCurrency = (amount: number): string => {
  return new Intl.NumberFormat('vi-VN').format(amount) + ' VNĐ';
};

export const formatDate = (dateString: string): string => {
  return new Date(dateString).toLocaleDateString('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  });
};

export const getTransactionsByType = (type: string): Transaction[] => {
  return allTransactions.filter(t => t.type === type);
};

export const getRecentTransactions = (limit: number = 5): Transaction[] => {
  return allTransactions
    .sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime())
    .slice(0, limit);
};
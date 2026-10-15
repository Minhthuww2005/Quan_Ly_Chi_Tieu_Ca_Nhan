import type { QuickAction } from '.././types';
import type { Transaction } from '.././types';
import type { User } from '.././types';

export const currentUser: User = {
  id: '1',
  name: 'Nguyễn Văn A',
  avatar: 'NVA'
};

export const quickActions: QuickAction[] = [
  {
    id: '1',
    title: 'Chuyển khoản',
    icon: '',
    color: 'bg-gray-700',
  },
  {
    id: '2',
    title: 'Thanh toán hóa đơn',
    icon: '',
    color: 'bg-gray-600',
  },
  {
    id: '3',
    title: 'Nạp tiền điện thoại',
    icon: '',
    color: 'bg-gray-800',
  },
  {
    id: '4',
    title: 'Nạp tiền',
    icon: '',
    color: 'bg-gray-600',
  },
  {
    id: '5',
    title: 'Mở sổ tiết kiệm',
    icon: '',
    color: 'bg-black',
  },
  {
    id: '6',
    title: 'Vay tín dụng',
    icon: '',
    color: 'bg-gray-500',
  },
  {
    id: '7',
    title: 'Mua bảo hiểm',
    icon: '',
    color: 'bg-gray-700',
  },
  {
    id: '8',
    title: 'Đầu tư chứng khoán',
    icon: '',
    color: 'bg-gray-900',
  },
  {
    id: '9',
    title: 'Lịch sử giao dịch',
    icon: '',
    color: 'bg-gray-600',
  }
];

export const recentTransactions: Transaction[] = [
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
    color: 'bg-red-600'
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
  }
];
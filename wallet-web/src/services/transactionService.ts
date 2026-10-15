/**
 * Transaction Service - Xử lý tất cả loại giao dịch
 */

export interface TransactionType {
  // Chuyển tiền
  TRANSFER: 'transfer';           // Chuyển ví sang ví
  BANK_TRANSFER: 'bank_transfer'; // Chuyển sang ngân hàng
  
  // Nạp tiền
  TOP_UP: 'top_up';               // Nạp tiền vào ví
  BANK_TOP_UP: 'bank_top_up';     // Nạp từ ngân hàng
  
  // Thanh toán
  PAYMENT: 'payment';             // Thanh toán hóa đơn
  BILL_PAYMENT: 'bill_payment';   // Hóa đơn (điện, nước, internet)
  PHONE_TOP_UP: 'phone_top_up';   // Nạp tiền điện thoại
  
  // Khác
  WITHDRAWAL: 'withdrawal';       // Rút tiền
  REFUND: 'refund';              // Hoàn tiền
  CASHBACK: 'cashback';          // Hoàn xu
}

export const TRANSACTION_TYPES = {
  // Chuyển tiền
  TRANSFER: 'transfer',
  BANK_TRANSFER: 'bank_transfer',
  
  // Nạp tiền
  TOP_UP: 'top_up',
  BANK_TOP_UP: 'bank_top_up',
  
  // Thanh toán
  PAYMENT: 'payment',
  BILL_PAYMENT: 'bill_payment',
  PHONE_TOP_UP: 'phone_top_up',
  
  // Khác
  WITHDRAWAL: 'withdrawal',
  REFUND: 'refund',
  CASHBACK: 'cashback'
} as const;

export const TRANSACTION_STATUS = {
  SUCCESS: 'success',
  PENDING: 'pending',
  FAILED: 'failed',
  PROCESSING: 'processing'
} as const;

// Icons cho từng loại giao dịch
export const TRANSACTION_ICONS = {
  transfer: '💸',
  bank_transfer: '🏦',
  top_up: '💰',
  bank_top_up: '🏧',
  payment: '🛒',
  bill_payment: '📄',
  phone_top_up: '📱',
  withdrawal: '💵',
  refund: '↩️',
  cashback: '🎁'
};

// Màu sắc cho từng loại
export const TRANSACTION_COLORS = {
  transfer: '#3B82F6',      // Blue
  bank_transfer: '#8B5CF6',  // Purple
  top_up: '#10B981',         // Green
  bank_top_up: '#059669',    // Dark Green
  payment: '#F59E0B',        // Orange
  bill_payment: '#EF4444',   // Red
  phone_top_up: '#EC4899',   // Pink
  withdrawal: '#6366F1',     // Indigo
  refund: '#14B8A6',         // Teal
  cashback: '#F97316'        // Deep Orange
};

// Tên hiển thị tiếng Việt
export const TRANSACTION_LABELS = {
  transfer: 'Chuyển tiền',
  bank_transfer: 'Chuyển đến ngân hàng',
  top_up: 'Nạp tiền',
  bank_top_up: 'Nạp từ ngân hàng',
  payment: 'Thanh toán',
  bill_payment: 'Thanh toán hóa đơn',
  phone_top_up: 'Nạp điện thoại',
  withdrawal: 'Rút tiền',
  refund: 'Hoàn tiền',
  cashback: 'Hoàn xu'
};

// Danh sách nhà mạng
export const PHONE_PROVIDERS = [
  { code: 'VIETTEL', name: 'Viettel', logo: '📡', color: '#E60012' },
  { code: 'VINAPHONE', name: 'VinaPhone', logo: '📱', color: '#0066CC' },
  { code: 'MOBIFONE', name: 'MobiFone', logo: '📲', color: '#FF6600' },
  { code: 'VIETNAMOBILE', name: 'Vietnamobile', logo: '📞', color: '#FFCC00' },
  { code: 'GMOBILE', name: 'Gmobile', logo: '☎️', color: '#00AA55' }
];

// Loại hóa đơn
export const BILL_TYPES = [
  { code: 'ELECTRIC', name: 'Tiền điện', icon: '💡', provider: 'EVN' },
  { code: 'WATER', name: 'Tiền nước', icon: '💧', provider: 'Nước sạch HN' },
  { code: 'INTERNET', name: 'Internet', icon: '🌐', provider: 'FPT/VNPT' },
  { code: 'TV', name: 'Truyền hình', icon: '📺', provider: 'VTVcab' },
  { code: 'INSURANCE', name: 'Bảo hiểm', icon: '🛡️', provider: 'Bảo Việt' },
  { code: 'TUITION', name: 'Học phí', icon: '🎓', provider: 'Trường học' }
];

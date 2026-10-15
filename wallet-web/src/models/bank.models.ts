/**
 * Linked Bank & Payment Models
 * PCI DSS Compliant - No raw card data stored
 */

// Linked Bank Collection Model (PCI DSS Compliant)
export interface LinkedBankModel {
  _id?: string; // MongoDB ObjectId
  bank_id: number; // ID duy nhất
  user_id: number; // References User.user_id - Khóa ngoại
  bank_name: string; // Tên ngân hàng (e.g., 'Vietcombank')
  account_last4: string; // 4 số cuối tài khoản (e.g., '9021') - ONLY for display
  payment_token: string; // Gateway token - CRITICAL, NEVER expose to frontend
  status: 'ACTIVE' | 'UNLINKED'; // Trạng thái liên kết
  is_default: boolean; // Tài khoản mặc định
  linked_at: Date; // Thời điểm liên kết
  updated_at: Date; // Cập nhật lần cuối
}

// Gateway Transaction Model (For tracking gateway operations)
export interface GatewayTransactionModel {
  _id?: string; // MongoDB ObjectId
  gateway_tx_id: string; // ID giao dịch từ Gateway
  user_id: number; // References User.user_id
  type: 'LINK_BANK' | 'TOPUP' | 'WITHDRAWAL'; // Loại giao dịch
  amount?: number; // Số tiền (optional for LINK_BANK)
  status: 'PENDING' | 'SUCCESS' | 'FAILED'; // Trạng thái
  gateway_response?: any; // Response từ Gateway (JSON)
  error_message?: string; // Lỗi nếu có
  created_at: Date; // Thời điểm tạo
}

// Top-up Request (Frontend → Backend)
export interface TopUpRequest {
  user_id: number;
  linked_bank_id: number; // ID của LinkedBank đã lưu
  amount: number;
  pin_code: string; // PIN xác thực
  description?: string;
}

// Withdrawal Request (Future feature)
export interface WithdrawalRequest {
  user_id: number;
  linked_bank_id: number;
  amount: number;
  pin_code: string;
  description?: string;
}

// Link Bank Request (Frontend → Backend)
export interface LinkBankRequest {
  user_id: number;
  gateway_transaction_id?: string; // Transaction ID từ Gateway UI (optional for new flow)
  bank_name?: string; // New flow: direct account info
  account_number?: string; // New flow: account number
  account_holder?: string; // New flow: account holder name
}

// Gateway Response Models (From Payment Gateway)
export interface GatewayLinkResponse {
  success: boolean;
  payment_token: string; // Secure token for future charges
  bank_name: string; // Tên ngân hàng
  last4_digits: string; // 4 số cuối
  account_type?: 'SAVINGS' | 'CHECKING'; // Loại tài khoản
}

export interface GatewayChargeResponse {
  success: boolean;
  transaction_id: string; // Gateway transaction ID
  status: 'SUCCESS' | 'FAILED' | 'PENDING';
  amount: number;
  message?: string;
  error_code?: string;
}

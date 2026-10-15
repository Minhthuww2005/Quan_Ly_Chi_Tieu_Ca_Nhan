// --- TRANSACTIONS & FINANCE ---

// Transaction Collection Model
export interface TransactionModel {
  _id?: string; // MongoDB ObjectId
  trans_id: number; // ID duy nhất của giao dịch
  wallet_id: number; // References Wallet.wallet_id - Khóa ngoại
  merchant_id?: number; // References Merchant.merchant_id (optional)
  timestamp: Date; // Thời điểm giao dịch
  amount: number; // Số tiền giao dịch - Use Decimal128 in MongoDB
  type: 'deposit' | 'withdrawal' | 'transfer' | 'payment' | 'loan' | 'investment' | 'insurance' | 'savings'; // Loại giao dịch
  status: 'success' | 'pending' | 'failed'; // Trạng thái giao dịch
  title: string; // Tiêu đề ngắn gọn của giao dịch
  description: string; // Mô tả chi tiết (ví dụ: nội dung chuyển khoản)
  icon?: string; // Icon liên quan đến loại giao dịch
  color?: string; // Màu sắc hiển thị trạng thái
  recipient?: string; // ID/Số tài khoản của người nhận (tùy chọn)
  reference?: string; // Mã tham chiếu hoặc mã hóa đơn (tùy chọn)
}

// Payment Collection Model
export interface PaymentModel {
  _id?: string; // MongoDB ObjectId
  pay_id: number; // ID duy nhất của thanh toán
  user_id: number; // References User.user_id - Khóa ngoại
  merchant_id: number; // References Merchant.merchant_id - Khóa ngoại
  amount: number; // Số tiền thanh toán - Use Decimal128 in MongoDB
  method: 'wallet' | 'card' | 'bank'; // Phương thức thanh toán
  date: Date; // Thời điểm thanh toán
  status: 'success' | 'pending' | 'failed'; // Trạng thái thanh toán
  description?: string; // Mô tả chi tiết
}

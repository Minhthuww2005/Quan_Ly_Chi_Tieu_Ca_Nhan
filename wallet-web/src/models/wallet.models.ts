// --- WALLET & FINANCE ---

// Wallet Collection Model
export interface WalletModel {
  _id?: string; // MongoDB ObjectId
  wallet_id: number; // ID duy nhất của ví
  user_id: number; // References User.user_id - Khóa ngoại
  balance: number; // Số dư hiện tại - Use Decimal128 in MongoDB
  currency: string; // Loại tiền tệ - Default: 'VND'
  status: 'active' | 'blocked' | 'closed'; // Trạng thái ví
  created_at: Date; // Ngày tạo
}

// Bank Account Collection Model
export interface BankAccountModel {
  _id?: string; // MongoDB ObjectId
  acc_id: number; // ID duy nhất của tài khoản
  user_id: number; // References User.user_id - Khóa ngoại
  acc_number: string; // Số tài khoản ngân hàng
  acc_type: 'savings' | 'checking'; // Loại tài khoản
  bank_name: string; // Tên ngân hàng
}

// Card Collection Model (BankCard)
export interface CardModel {
  _id?: string; // MongoDB ObjectId
  card_id: number; // ID duy nhất của thẻ
  user_id: number; // References User.user_id - Khóa ngoại
  card_no: string; // Số thẻ - Encrypted
  card_type: 'debit' | 'credit'; // Loại thẻ
  cvv_hash: string; // CVV đã hash - Never store plain
  expiry_date: Date; // Ngày hết hạn
  holder: string; // Tên chủ thẻ
  balance?: number; // Số dư hiện tại (chỉ áp dụng cho thẻ debit)
  limit?: number; // Hạn mức tín dụng (chỉ áp dụng cho credit)
  status: 'active' | 'blocked' | 'expired'; // Trạng thái của thẻ
}

// Savings Account Collection Model
export interface SavingsAccountModel {
  _id?: string; // MongoDB ObjectId
  id: string; // ID duy nhất của tài khoản tiết kiệm
  user_id: number; // References User.user_id - Khóa ngoại
  name: string; // Tên gói tiết kiệm (ví dụ: "Tiết kiệm linh hoạt")
  balance: number; // Số tiền hiện có trong tài khoản
  interestRate: number; // Lãi suất (%/năm)
  term: number; // Kỳ hạn gửi (tính bằng tháng)
  startDate: string; // Ngày bắt đầu gửi
  maturityDate: string; // Ngày đáo hạn
  status: 'active' | 'matured' | 'closed'; // Trạng thái tài khoản
}

// Loan Account Collection Model
export interface LoanAccountModel {
  _id?: string; // MongoDB ObjectId
  id: string; // ID duy nhất của khoản vay
  user_id: number; // References User.user_id - Khóa ngoại
  type: string; // Loại khoản vay (ví dụ: "Vay tiêu dùng", "Vay mua nhà")
  amount: number; // Tổng số tiền vay ban đầu
  remainingAmount: number; // Số tiền còn lại cần thanh toán
  interestRate: number; // Lãi suất (%/năm)
  term: number; // Kỳ hạn vay (tính bằng tháng)
  monthlyPayment: number; // Số tiền trả hàng tháng
  startDate: string; // Ngày bắt đầu vay
  nextPaymentDate: string; // Ngày thanh toán tiếp theo
  status: 'active' | 'paid' | 'overdue'; // Trạng thái khoản vay
}

// Insurance Collection Model
export interface InsuranceModel {
  _id?: string; // MongoDB ObjectId
  id: string; // ID duy nhất của hợp đồng bảo hiểm
  user_id: number; // References User.user_id - Khóa ngoại
  type: string; // Loại bảo hiểm (ví dụ: "Sức khỏe", "Tài sản")
  name: string; // Tên sản phẩm bảo hiểm
  premium: number; // Phí bảo hiểm định kỳ
  coverage: number; // Mức bảo hiểm tối đa
  startDate: string; // Ngày bắt đầu hiệu lực
  endDate: string; // Ngày kết thúc hiệu lực
  status: 'active' | 'expired' | 'cancelled'; // Trạng thái hợp đồng
}

// Investment Collection Model
export interface InvestmentModel {
  _id?: string; // MongoDB ObjectId
  id: string; // ID duy nhất của tài sản đầu tư
  user_id: number; // References User.user_id - Khóa ngoại
  type: string; // Loại hình đầu tư (ví dụ: "Cổ phiếu", "Quỹ", "Vàng", "Tiền điện tử")
  name: string; // Tên tài sản/cổ phiếu (ví dụ: "Bitcoin", "VNM")
  amount: number; // Số tiền ban đầu đã đầu tư (ví dụ: 5,000,000 VND)
  quantity: number; // Số lượng tài sản mua được (ví dụ: 0.00228 BTC)
  purchasePrice: number; // Giá mua tại thời điểm đầu tư (ví dụ: 2,194,682,400 VND/BTC)
  currentValue: number; // Giá trị hiện tại của khoản đầu tư (quantity * currentPrice)
  profit: number; // Lợi nhuận tuyệt đối (CurrentValue - Amount)
  profitPercent: number; // Tỷ lệ lợi nhuận (%)
  purchaseDate: string; // Ngày mua
  status: 'active' | 'sold'; // Trạng thái đầu tư
}

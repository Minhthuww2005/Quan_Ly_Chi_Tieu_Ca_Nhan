// --- CORE USER & UI ---

export interface User {
  id: string; // ID duy nhất của người dùng
  name: string; // Tên đầy đủ của người dùng
  avatar: string; // URL ảnh đại diện
  phone?: string; // Số điện thoại (tùy chọn)
  email?: string; // Địa chỉ email (tùy chọn)
  accountNumber?: string; // Số tài khoản chính của ví
  balance?: number; // Số dư hiện tại trong ví chính
}

export interface QuickAction {
  id: string; // ID duy nhất của hành động nhanh
  title: string; // Tiêu đề hiển thị trên giao diện (ví dụ: "Chuyển tiền")
  icon: string; // Tên icon hiển thị
  color: string; // Mã màu cho icon hoặc nền
}

// --- TRANSACTIONS & FINANCE ---

export interface Transaction {
  id: string; // ID duy nhất của giao dịch
  type: 'deposit' | 'withdrawal' | 'transfer' | 'payment' | 'loan' | 'investment' | 'insurance' | 'savings'; // Loại giao dịch
  title: string; // Tiêu đề ngắn gọn của giao dịch
  description: string; // Mô tả chi tiết (ví dụ: nội dung chuyển khoản)
  amount: number; // Số tiền giao dịch
  date: string; // Thời điểm giao dịch
  status: 'success' | 'pending' | 'failed'; // Trạng thái giao dịch
  icon: string; // Icon liên quan đến loại giao dịch
  color: string; // Màu sắc hiển thị trạng thái
  recipient?: string; // ID/Số tài khoản của người nhận (tùy chọn)
  reference?: string; // Mã tham chiếu hoặc mã hóa đơn (tùy chọn)
}

export interface BankCard {
  id: string; // ID duy nhất của thẻ liên kết
  type: 'debit' | 'credit'; // Loại thẻ
  number: string; // Số thẻ (thường bị che một phần)
  holder: string; // Tên chủ thẻ
  expiry: string; // Ngày hết hạn (MM/YY)
  balance: number; // Số dư hiện tại của thẻ (chỉ áp dụng cho thẻ debit)
  limit?: number; // Hạn mức tín dụng (chỉ áp dụng cho thẻ credit, tùy chọn)
  status: 'active' | 'blocked' | 'expired'; // Trạng thái của thẻ
}

export interface SavingsAccount {
  id: string; // ID duy nhất của tài khoản tiết kiệm
  name: string; // Tên gói tiết kiệm (ví dụ: "Tiết kiệm linh hoạt")
  balance: number; // Số tiền hiện có trong tài khoản
  interestRate: number; // Lãi suất (%/năm)
  term: number; // Kỳ hạn gửi (tính bằng tháng)
  startDate: string; // Ngày bắt đầu gửi
  maturityDate: string; // Ngày đáo hạn
  status: 'active' | 'matured' | 'closed'; // Trạng thái tài khoản
}

export interface LoanAccount {
  id: string; // ID duy nhất của khoản vay
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

export interface Insurance {
  id: string; // ID duy nhất của hợp đồng bảo hiểm
  type: string; // Loại bảo hiểm (ví dụ: "Sức khỏe", "Tài sản")
  name: string; // Tên sản phẩm bảo hiểm
  premium: number; // Phí bảo hiểm định kỳ
  coverage: number; // Mức bảo hiểm tối đa
  startDate: string; // Ngày bắt đầu hiệu lực
  endDate: string; // Ngày kết thúc hiệu lực
  status: 'active' | 'expired' | 'cancelled'; // Trạng thái hợp đồng
}

export interface Investment {
  id: string; // ID duy nhất của tài sản đầu tư
  type: string; // Loại hình đầu tư (ví dụ: "Cổ phiếu", "Quỹ", "Vàng")
  name: string; // Tên tài sản/cổ phiếu
  amount: number; // Số tiền ban đầu đã đầu tư
  currentValue: number; // Giá trị hiện tại của khoản đầu tư
  profit: number; // Lợi nhuận tuyệt đối (CurrentValue - Amount)
  profitPercent: number; // Tỷ lệ lợi nhuận (%)
  purchaseDate: string; // Ngày mua
  status: 'active' | 'sold'; // Trạng thái đầu tư
}

// --- SECURITY & COMPLIANCE (Bảo mật & Tuân thủ) ---

export interface KYCProfile {
  id: string; // ID duy nhất của hồ sơ KYC
  userId: string; // Khóa ngoại, ID của người dùng sở hữu hồ sơ
  idType: 'national_id' | 'passport' | 'driver_license'; // Loại giấy tờ tùy thân
  idNumber: string; // Số giấy tờ tùy thân
  frontImageUrl: string; // URL ảnh mặt trước giấy tờ
  backImageUrl: string; // URL ảnh mặt sau giấy tờ
  verificationStatus: 'pending' | 'verified' | 'rejected'; // Trạng thái xác minh
  verifiedBy?: string; // ID của cán bộ kiểm duyệt (tùy chọn)
  verifiedAt?: string; // Thời điểm xác minh thành công (tùy chọn)
}

export interface SecurityLog {
  id: string; // ID duy nhất của nhật ký bảo mật
  userId: string; // Khóa ngoại, ID của người dùng
  eventType: 'login' | 'logout' | 'password_change' | 'device_added' | 'transaction_fail'; // Loại sự kiện bảo mật
  ipAddress: string; // Địa chỉ IP thực hiện hành động
  deviceInfo: string; // Thông tin thiết bị (ví dụ: trình duyệt, HĐH)
  timestamp: string; // Thời điểm xảy ra sự kiện
  status: 'success' | 'failed'; // Kết quả hành động
}

export interface Token {
  id: string; // ID duy nhất của mã thông báo
  userId: string; // Khóa ngoại, ID của người dùng
  tokenValue: string; // Mã thông báo (ví dụ: mã OTP, mã reset)
  tokenType: 'reset_password' | 'mfa_verification' | 'email_verification'; // Mục đích của token
  expiresAt: string; // Thời điểm token hết hạn
  isUsed: boolean; // Đã được sử dụng hay chưa
}

// --- PAYMENT PARTNERS (Đối tác Thanh toán) ---

export interface Merchant {
  id: string; // ID duy nhất của đơn vị chấp nhận thanh toán (Merchant ID)
  name: string; // Tên doanh nghiệp/cửa hàng
  businessAddress: string; // Địa chỉ kinh doanh
  taxId: string; // Mã số thuế
  merchantType: string; // Loại hình kinh doanh (ví dụ: "Ăn uống", "Bán lẻ")
  status: 'active' | 'suspended'; // Trạng thái hoạt động
  settlementBankAcc: string; // Tài khoản ngân hàng nhận tiền thanh toán của Merchant
  rating: number; // Xếp hạng của Merchant (tùy chọn)
}

export interface Biller {
  id: string; // ID duy nhất của nhà cung cấp hóa đơn
  name: string; // Tên nhà cung cấp (ví dụ: EVN, Viettel)
  serviceType: 'electricity' | 'water' | 'internet' | 'mobile'; // Loại dịch vụ cung cấp
  billerCode: string; // Mã số định danh của nhà cung cấp trong hệ thống
  apiEndpoint: string; // Địa chỉ API để tra cứu/thanh toán hóa đơn
  status: 'active' | 'maintenance'; // Trạng thái dịch vụ
}

// --- USER EXPERIENCE (Trải nghiệm người dùng) ---

export interface Notification {
  id: string; // ID duy nhất của thông báo
  userId: string; // Khóa ngoại, ID của người nhận thông báo
  type: 'transaction_alert' | 'marketing' | 'system_warning'; // Loại thông báo
  message: string; // Nội dung thông báo
  deepLink?: string; // Đường dẫn nội bộ để dẫn người dùng đến màn hình liên quan (tùy chọn)
  createdAt: string; // Thời điểm tạo thông báo
  isSeen: boolean; // Trạng thái đã xem hay chưa
}

export interface Contact {
  id: string; // ID duy nhất của mục trong danh bạ
  ownerId: string; // Khóa ngoại, ID của người dùng sở hữu danh bạ này
  recipientName: string; // Tên người nhận
  accountNumber: string; // Số tài khoản/số ví của người nhận
  bankName: string; // Tên ngân hàng/tên ví của người nhận
  nickname?: string; // Tên gợi nhớ (tùy chọn)
  lastUsedDate: string; // Ngày giao dịch gần nhất với người này
}
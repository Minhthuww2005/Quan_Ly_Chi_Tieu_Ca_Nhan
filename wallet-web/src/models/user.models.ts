// --- CORE USER & UI ---

// User Collection Model
export interface UserModel {
  _id?: string; // MongoDB ObjectId
  user_id: number; // Unique identifier
  name: string; // Tên đầy đủ của người dùng
  email: string; // Địa chỉ email
  phone?: string; // Số điện thoại (optional cho Google Sign-In)
  password?: string; // Hashed password (optional cho Google Sign-In)
  role: 'user' | 'admin' | 'merchant'; // Vai trò
  avatar?: string; // URL ảnh đại diện
  // Google OAuth fields
  googleId?: string; // Google unique identifier
  authProvider?: 'local' | 'google'; // Phương thức đăng ký
  isEmailVerified?: boolean; // Email đã xác thực (tự động true với Google)
  // KYC & Security fields
  kycTier?: 'TIER_1' | 'TIER_2'; // KYC tier for transfer limits
  accountStatus?: 'ACTIVE' | 'FROZEN' | 'SUSPICIOUS'; // Account fraud status
  pinHash?: string; // Hashed PIN for transaction authorization
  created_at: Date;
  updated_at: Date;
}

// Personal Information Collection Model
export interface PersonalInformationModel {
  _id?: string; // MongoDB ObjectId
  person_id: number; // ID duy nhất của hồ sơ
  user_id: number; // References User.user_id - Khóa ngoại
  passport: string; // Số hộ chiếu
  address: string; // Địa chỉ
}

// KYC Profile Collection Model
export interface KYCProfileModel {
  _id?: string; // MongoDB ObjectId
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

// Security Log Collection Model
export interface SecurityLogModel {
  _id?: string; // MongoDB ObjectId
  id: string; // ID duy nhất của nhật ký bảo mật
  userId: string; // Khóa ngoại, ID của người dùng
  eventType: 'login' | 'logout' | 'password_change' | 'device_added' | 'transaction_fail'; // Loại sự kiện bảo mật
  ipAddress: string; // Địa chỉ IP thực hiện hành động
  deviceInfo: string; // Thông tin thiết bị (ví dụ: trình duyệt, HĐH)
  timestamp: string; // Thời điểm xảy ra sự kiện
  status: 'success' | 'failed'; // Kết quả hành động
}

// Token Collection Model
export interface TokenModel {
  _id?: string; // MongoDB ObjectId
  id: string; // ID duy nhất của mã thông báo
  userId: string; // Khóa ngoại, ID của người dùng
  tokenValue: string; // Mã thông báo (ví dụ: mã OTP, mã reset)
  tokenType: 'reset_password' | 'mfa_verification' | 'email_verification'; // Mục đích của token
  expiresAt: string; // Thời điểm token hết hạn
  isUsed: boolean; // Đã được sử dụng hay chưa
}

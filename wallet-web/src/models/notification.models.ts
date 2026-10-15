// --- USER EXPERIENCE (Trải nghiệm người dùng) ---

// Notification Collection Model
export interface NotificationModel {
  _id?: string; // MongoDB ObjectId
  notification_id: number; // ID duy nhất của thông báo
  user_id: number; // References User.user_id - Khóa ngoại
  type: 'transaction_alert' | 'marketing' | 'system_warning'; // Loại thông báo
  message: string; // Nội dung thông báo
  deepLink?: string; // Đường dẫn nội bộ để dẫn người dùng đến màn hình liên quan (tùy chọn)
  createdAt: Date; // Thời điểm tạo thông báo
  isSeen: boolean; // Trạng thái đã xem hay chưa
}

// Contact Collection Model
export interface ContactModel {
  _id?: string; // MongoDB ObjectId
  id: string; // ID duy nhất của mục trong danh bạ
  ownerId: string; // Khóa ngoại, ID của người dùng sở hữu danh bạ này
  recipientName: string; // Tên người nhận
  accountNumber: string; // Số tài khoản/số ví của người nhận
  bankName: string; // Tên ngân hàng/tên ví của người nhận
  nickname?: string; // Tên gợi nhớ (tùy chọn)
  lastUsedDate: string; // Ngày giao dịch gần nhất với người này
}

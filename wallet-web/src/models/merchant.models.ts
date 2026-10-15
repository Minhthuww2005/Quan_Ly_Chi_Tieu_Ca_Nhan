// --- PAYMENT PARTNERS (Đối tác Thanh toán) ---

// Merchant Collection Model
export interface MerchantModel {
  _id?: string; // MongoDB ObjectId
  merchant_id: number; // ID duy nhất của merchant
  name: string; // Tên doanh nghiệp/cửa hàng
  location: string; // Vị trí địa lý
  rating: number; // Xếp hạng của Merchant (0.0 - 5.0)
  businessAddress: string; // Địa chỉ kinh doanh
  taxId: string; // Mã số thuế
  merchantType: string; // Loại hình kinh doanh (ví dụ: "Ăn uống", "Bán lẻ")
  status: 'active' | 'suspended'; // Trạng thái hoạt động
  settlementBankAcc: string; // Tài khoản ngân hàng nhận tiền thanh toán của Merchant
  created_at: Date; // Ngày tạo
}

// Biller Collection Model
export interface BillerModel {
  _id?: string; // MongoDB ObjectId
  id: string; // ID duy nhất của nhà cung cấp hóa đơn
  name: string; // Tên nhà cung cấp (ví dụ: EVN, Viettel)
  serviceType: 'electricity' | 'water' | 'internet' | 'mobile'; // Loại dịch vụ cung cấp
  billerCode: string; // Mã số định danh của nhà cung cấp trong hệ thống
  apiEndpoint: string; // Địa chỉ API để tra cứu/thanh toán hóa đơn
  status: 'active' | 'maintenance'; // Trạng thái dịch vụ
}

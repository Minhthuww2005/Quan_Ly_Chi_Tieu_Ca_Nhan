# Nội dung chuyển dữ liệu HTD PAY sang Neon

Nguồn: file wallet_export.json trong thư mục đồ án người dùng cung cấp.
Đích: database neondb trên Neon đã được người dùng chỉ định; schema mới htdpay.

Tổng cộng 31 collection, 193 bản ghi. Không sửa hoặc xóa các bảng public hiện tại.

Dữ liệu gồm tài khoản, hash mật khẩu/PIN, ví và số dư, giao dịch, tài khoản ngân hàng mô phỏng, thông tin thẻ và hồ sơ KYC. Những trường mang tên encrypted trong bản nguồn cần được kiểm tra định dạng; các giá trị chưa mã hóa sẽ được bảo vệ khi nhập. File gốc và cấu hình kết nối nằm trong danh sách Git ignore.

| Collection | Số bản ghi |
|---|---:|
| gatewaytransactions | 11 |
| simpletransactions | 5 |
| cards | 2 |
| securitylogs | 0 |
| wallets | 5 |
| linkedbanks | 1 |
| simplecards | 3 |
| bankaccounts | 50 |
| transactions | 22 |
| simpleinvestments | 3 |
| supportedbanks | 40 |
| tokens | 0 |
| users | 7 |
| simplebillers | 4 |
| investments | 4 |
| simplesavings | 2 |
| simpleinsurances | 2 |
| kycprofiles | 1 |
| loanaccounts | 2 |
| savingsaccounts | 2 |
| insurances | 0 |
| simplemerchants | 4 |
| contacts | 3 |
| merchants | 4 |
| notifications | 4 |
| billers | 4 |
| personalinformations | 0 |
| simplenotifications | 3 |
| payments | 0 |
| simpleloans | 2 |
| simplecontacts | 3 |

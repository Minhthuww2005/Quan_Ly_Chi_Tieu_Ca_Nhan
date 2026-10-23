# Chi tiêu nhóm trong HTD PAY

Phần nhóm là chức năng bổ sung cho HTD PAY, sử dụng tài khoản, JWT, PostgreSQL và Controller–Service–Repository của ứng dụng ví. Nhánh main ở commit 6061020 đã nằm trong lịch sử bản hiện tại, nhưng module RevenueFlow trên nhánh đó chỉ mở nghiệp vụ Income; không có màn hình thu chi nhóm cho tài khoản HTD PAY. Vì vậy không thể chỉ ghép giao diện cũ rồi coi là đã tích hợp.

## Sử dụng

- Client: đăng nhập, chọn **Nhóm** ở thanh điều hướng dưới cùng (`/groups`). Tạo nhóm, đặt ngân sách tháng, thêm thành viên bằng email tài khoản HTD PAY đang hoạt động. Ghi khoản thu/chi, chọn người thanh toán và tháng thống kê. Trưởng nhóm sửa thông tin nhóm và quản lý thành viên; thành viên sửa/xóa khoản do mình tạo. Trưởng nhóm cũng có quyền sửa/xóa các khoản trong nhóm.
- Admin: dashboard có mục **Chi tiêu nhóm** (`/admin/groups`). Xem nhóm, thành viên, thu/chi, ngân sách; lưu trữ hoặc mở lại nhóm. Admin không dùng màn hình này để sửa các khoản của thành viên. Mỗi thay đổi trạng thái được ghi vào securitylogs.
- Ngân sách áp dụng cho từng tháng; giá trị 0 nghĩa là chưa đặt giới hạn. Vượt ngân sách hiện cảnh báo, không chặn ghi nhận khoản chi đã xảy ra.
- Đây là sổ theo dõi chung; thao tác ghi sổ không thực hiện chuyển tiền, chia nợ hay thanh toán và không sửa số dư ví cá nhân.

## Lưu dữ liệu và phân quyền

Backend bổ sung ba bảng `htdpay.expensegroups`, `expensemembers`, `expenseentries`, chỉ mục định danh/thành viên và phiên bản schema 2. Khởi động API thực hiện migration bổ sung có khóa; không xóa hoặc nhập lại dữ liệu ví. Tài khoản database cần quyền tạo bảng/chỉ mục trong schema htdpay ở lần đầu cập nhật. Các khoản xóa được giữ dưới dạng deleted để bảo toàn lịch sử.

Controller sử dụng transaction ghi và advisory lock đang có. Service kiểm tra tư cách thành viên từ database trên mỗi yêu cầu; người ngoài nhận 404. Việc thu hồi quyền có hiệu lực ở lần gọi tiếp theo. Sửa khoản thu chi, sửa nhóm và đổi trạng thái yêu cầu version; phiên bản cũ nhận 409. Read GET không ghi dữ liệu.

| API | Quyền |
|---|---|
| GET/POST `/api/groups` | Danh sách của mình / tạo nhóm |
| GET/PATCH `/api/groups/{id}` | Thành viên / trưởng nhóm |
| POST `/api/groups/{id}/members` | Trưởng nhóm; body email |
| DELETE `/api/groups/{id}/members/{userId}` | Trưởng nhóm; không xóa chính trưởng nhóm |
| POST `/api/groups/{id}/entries` | Thành viên; nhóm đang hoạt động |
| PUT/DELETE `/api/groups/{id}/entries/{entryId}` | Người tạo hoặc trưởng nhóm; body version |
| GET `/api/admin/groups` và `/{id}` | Admin |
| PATCH `/api/admin/groups/{id}/status` | Admin; body archived và version |

GET chi tiết hỗ trợ `?month=yyyy-MM`, trả danh sách thành viên, các khoản trong tháng, tổng thu, tổng chi, chênh lệch, cảnh báo ngân sách và tổng chi theo danh mục. Bản đầu chưa có mời thành viên cần chấp nhận, chia nợ hoặc lịch thu chi định kỳ. Không dùng kết quả module này để khẳng định EF Core, Redis, SignalR hay worker đã tích hợp vào HTD PAY.

## Kiểm thử

`dotnet run --project scripts/WalletGroupTests` kiểm tra service thật với repository bộ nhớ; không chứng minh kết nối PostgreSQL hoặc giao diện trình duyệt.

`scripts/wallet-group-http-tests.py --local-admin-container htdpay-groups-qa-15439` kiểm tra HTTP trên database PostgreSQL QA riêng; chỉ chấp nhận localhost và container QA được đặt tên rõ. Bộ này tạo tài khoản QA mới, kiểm tra tranh chấp hai yêu cầu sửa cùng version, phân quyền và số dư ví không đổi. Không chạy trên Neon.

Frontend: `cd wallet-web; pnpm install --frozen-lockfile; pnpm dev`. API vẫn dùng cổng 5081; giao diện 5174. Cấu hình thật lấy từ `.env.wallet` hoặc biến môi trường riêng, không commit secret. Hướng dẫn chung ở WALLET_MIGRATION.md.

### Kết quả ngày 23/10/2026

- Backend build: 0 lỗi, 0 cảnh báo. Frontend TypeScript + Vite build thành công; Vite cảnh báo bundle chính lớn hơn 500 kB, cần chia tải khi tối ưu hiệu năng.
- Service: 50 kiểm tra đạt với repository bộ nhớ.
- HTTP: 38 kiểm tra đạt trên PostgreSQL 17 QA riêng; hai cập nhật cùng version trả lần lượt 200 và 409; số dư ví cá nhân không đổi. Có tạo tài khoản QA và sổ nhóm trong database local riêng, không dùng dữ liệu Neon để chạy bộ ghi thử này.
- Kiểm thử database trống phát hiện và đã sửa việc initializer cũ tạo owner_id index trên bảng nhóm mới. Bảng nhóm dùng migration riêng; các bảng ví gốc giữ nguyên.
- Chưa xác nhận thao tác bằng trình duyệt hoặc bố cục trực quan: công cụ điều khiển trình duyệt không phản hồi trong phiên này. Kết quả build và HTTP không thay thế kiểm thử giao diện tương tác.

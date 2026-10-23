# HTD PAY chuyển sang ASP.NET Core và Neon

## Kết quả

Frontend React/TypeScript được chuyển từ đồ án Wallet-Funding sang `wallet-web`, giữ bố cục và các trang gốc. Backend mới ở `src/WalletFunding.Api` dùng ASP.NET Core 10, Npgsql, JWT và BCrypt; không cần Express hoặc MongoDB để chạy bản này. Frontend gọi `/api` qua proxy Vite đến cổng 5081. Backend đã tách Controller–Service–Repository; xem [kiến trúc và luồng OOP](WALLET_ARCHITECTURE.md).

Các nhóm chức năng đã triển khai: đăng ký/đăng nhập, mật khẩu và PIN; ví và chuyển tiền; liên kết ngân hàng, nạp ví và thẻ; nạp điện thoại và hóa đơn; tiết kiệm, đầu tư, vay và bảo hiểm; thông báo, danh bạ; quản trị người dùng, giao dịch và xét duyệt KYC. Các modal đăng ký dịch vụ đã được nối với API thay cho thông báo thành công giả.

## Database

Đã nhập 193 bản ghi từ 31 collection trong `wallet_export.json` vào schema `htdpay` của Neon theo xác nhận của người dùng. Đối chiếu từng collection khớp số lượng nguồn. Schema có 31 bảng dữ liệu và bảng `schema_versions`. Số lượng bản ghi của 13 bảng `public` hiện có không đổi sau nhập.

Mỗi bảng dùng khóa `record_key`, dữ liệu JSONB và các chỉ mục phục vụ định danh/chủ sở hữu. Đây là cách ánh xạ tương thích dữ liệu đồ án cũ, chưa phải thiết kế quan hệ chuẩn hóa bằng EF Core. PostgresWalletRepository tải collection vào bộ nhớ trong một request và dùng transaction cùng advisory lock cho thao tác ghi. Phù hợp dữ liệu demo hiện tại; cần chuẩn hóa bảng, phân trang SQL và giảm phạm vi khóa khi mở rộng.

Các trường nhạy cảm chưa mã hóa được bảo vệ khi nhập bằng ASP.NET Core Data Protection. Hash mật khẩu/PIN được giữ để tương thích tài khoản cũ. Giữ an toàn thư mục khóa `.wallet-keys` khi chuyển máy; mất khóa sẽ không giải mã được dữ liệu do bản mới bảo vệ. File kết nối, khóa và dữ liệu kiểm thử không được đưa lên Git.

## Chạy

Từ thư mục gốc, chạy `powershell -File scripts/start-wallet.ps1`. Script đọc cấu hình Neon từ `.env.wallet` đã lưu trên máy. Mẫu cấu hình nằm trong `.env.wallet.example`; không lưu thông tin thật vào file mẫu. Không cần nhập lại dữ liệu mỗi lần chạy.

Trong `wallet-web`, chạy `pnpm dev`. Giao diện: http://localhost:5174. API: http://localhost:5081/api/health.

Để kiểm thử local: Docker Desktop phải chạy; dùng `powershell -File scripts/start-wallet.ps1 -Local`, sau đó chạy `python scripts/wallet-smoke.py`. Bộ kiểm thử chỉ tạo tài khoản QA trên PostgreSQL local. Dừng backend Neon trước khi chạy local vì dùng chung cổng 5081.

## Kiểm chứng và giới hạn

Bản cập nhật Google OAuth, VNPay sandbox và kiểm thử 137 ca HTTP được mô tả trong [WALLET_TEST_REPORT.md](WALLET_TEST_REPORT.md). Bảo hiểm demo hiện thu phí khi tạo và gia hạn. VNPay có checkout, Return URL và IPN xác minh chữ ký; cần URL backend công khai để nhận IPN thật. Chưa kiểm chứng đăng nhập Google bằng tài khoản thật hoặc giao dịch ngân hàng sandbox đầy đủ.



## Bổ sung chi tiêu nhóm

Client `/groups` và admin `/admin/groups` đã được nối vào menu HTD PAY. Backend tự bổ sung ba bảng nhóm trong schema htdpay khi khởi động; không cần nhập lại dữ liệu ví. Xem [phạm vi, phân quyền và kiểm thử nhóm](WALLET_GROUP_EXPENSES.md).

## Tích hợp kỹ thuật vào chi tiêu nhóm

Luồng nhóm sử dụng EF Core trên JSONB hiện có, Redis cache có phiên bản, outbox với Channel/BackgroundService và SignalR client/admin. Bản này bổ sung bảng group_outbox và schema version 3; giữ nguyên dữ liệu ví. Xem [cơ chế, cấu hình và giới hạn](HTDPAY_FRAMEWORK_INTEGRATION.md). Redis local cần được chạy và Cache__Redis trỏ đúng cổng; header BYPASS không được tính là bằng chứng Redis HIT.

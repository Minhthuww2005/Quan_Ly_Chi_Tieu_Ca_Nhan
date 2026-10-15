# HTD PAY — kiểm thử ngày 13/10/2026

## Kết quả đã chạy

- ASP.NET Core build: thành công, 0 lỗi, 0 cảnh báo.
- TypeScript: thành công. Vite production build: thành công; còn cảnh báo bundle lớn (874 kB trước gzip).
- `scripts/WalletProviderTests`: 15 kiểm tra Google JWT (chữ ký, issuer, audience, expiry, email_verified, sub, thuật toán) và VNPay (canonicalization, HMAC, sửa payload, URL sandbox).
- `scripts/wallet-extended-tests.py`: 137 ca HTTP đạt trên PostgreSQL local, API QA cổng 5082. Bao gồm 85 ca cơ bản và 52 ca mở rộng. Dữ liệu QA không dùng Neon.
- Callback VNPay trong bộ HTTP được ký độc lập bằng Python với secret QA riêng. Đây không phải bằng chứng một giao dịch ngân hàng sandbox thực tế đã hoàn tất.

## Nhóm chức năng đã kiểm tra bằng API

Đăng ký, đăng nhập, PIN, token đặt lại mật khẩu một lần; quyền admin và quyền sở hữu dữ liệu; chuyển tiền đồng thời bảo toàn số dư; ngân hàng/thẻ; nạp điện thoại, hóa đơn; tiết kiệm/đầu tư/vay/bảo hiểm; KYC và quản trị. Bộ mở rộng kiểm tra cập nhật hồ sơ, chặn tự nâng role, idempotency giao dịch, hóa đơn trùng và VNPay.

VNPay được kiểm tra: sai PIN; chữ ký giả; sai merchant; thiếu đơn; sai số tiền; Return URL không cộng tiền; IPN đồng thời chỉ cộng một lần; giao dịch thất bại không đổi số dư; đơn không thuộc người dùng bị chặn; hóa đơn đang chờ không được thanh toán lại; hóa đơn trả bằng ngân hàng ngoài không trừ thêm ví.

## Thay đổi giao diện

Nối nút chuyển tiền trong thẻ số dư vào modal chuyển ví; nối nút thêm thẻ; thêm khóa/mở thẻ và xóa thẻ; sửa hiển thị dấu tiền trong lịch sử và số dư sau nạp; thêm trang cài đặt, thông báo, bảo hiểm, thanh toán và kết quả VNPay; kiểm tra phiên đăng nhập với backend; chống gửi trùng thao tác tiền.

Ảnh KYC gửi lại phải được quản trị viên xem xét. Không coi tải ảnh lên là đã xác minh sinh trắc học. Bảo hiểm demo thu phí khi tạo/gia hạn; không phải hợp đồng bảo hiểm thực tế.

## Cấu hình và kiểm tra còn cần bên ngoài

Google Client ID và VNPay sandbox merchant được đọc từ `.env.wallet` (Git ignore). Không ghi secret vào tài liệu, log hoặc frontend. Google cần đăng ký đúng JavaScript origins `http://localhost:5174` và `http://127.0.0.1:5174`. Chưa kiểm chứng đăng nhập bằng tài khoản Google thật của người dùng.

VNPay Return URL hiện là `http://127.0.0.1:5174/payments/return`. IPN là `https://<backend-public>/api/payments/vnpay/ipn`. Cần URL HTTPS backend công khai và đăng ký với VNPay trước khi thử một giao dịch sandbox đầy đủ. Localhost không nhận được IPN từ máy chủ VNPay. Return URL chỉ hiển thị kết quả có chữ ký; số dư chỉ cập nhật khi IPN hợp lệ được backend xử lý.

Tham khảo tích hợp: https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html

Không tuyên bố hoàn thiện 100%: chưa có giao dịch ngân hàng sandbox thực tế, chưa đăng nhập Google bằng tài khoản thật, chưa kiểm thử trực quan mọi trang và mọi kích thước màn hình. Ngân hàng/hóa đơn/nạp điện thoại/vay/bảo hiểm ngoài VNPay vẫn là mô phỏng đồ án.

## Chạy lại

1. Khởi động Docker Desktop, chạy `powershell -File scripts/start-wallet.ps1 -Local -Port 5082 -TestProviders`.
2. Chạy `python scripts/wallet-extended-tests.py`. Bộ mở rộng chờ qua cửa sổ rate limit giữa hai nhóm kiểm tra, không tắt giới hạn của API.
3. Chạy `dotnet run --project scripts/WalletProviderTests`.
4. Backend dùng Neon: `powershell -File scripts/start-wallet.ps1`. Frontend trong `wallet-web`: `pnpm dev`, cổng 5174.

Kiểm thử bổ sung đã đạt: ảnh KYC không tự nâng trạng thái xác minh, chặn upload sai chủ sở hữu, ảnh mới quay về chờ duyệt; tạo/gia hạn bảo hiểm trừ đúng phí. Bốn ca frontend transport cũng đạt, gồm sai PIN giữ phiên, token hết hạn xóa phiên, retry giữ Idempotency-Key và gửi đồng thời chỉ gọi mạng một lần.

Kiểm tra giao diện thực tế: đăng nhập client demo thành công, nút Google hiển thị với Client ID đã cung cấp, trang VNPay hiển thị và nút chuyển tiền mở đúng modal chuyển ví. Ảnh minh chứng lưu riêng tại wallet-test-results/vnpay-ui.png (Git ignore).


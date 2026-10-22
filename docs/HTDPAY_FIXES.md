# HTD PAY — sửa luồng tiền, callback, typed validation và đo dữ liệu lớn

Nhánh `fix/htdpay-money-flow`, tách từ `develop` (`bf21d48`). Tài liệu này ghi lại lỗi, cách sửa và
bằng chứng kiểm thử, theo các ưu tiên giảng viên nêu ở tuần 6: niêm phong source/output; hoàn thiện
luồng tiền, callback, typed validation và phép đo dữ liệu lớn; CI và tải production chưa xác nhận.

## 1. Luồng tiền

| Lỗi trước khi sửa | Sửa |
|---|---|
| `investments/sell` cộng `sellPrice` do client gửi, nên client tự tạo được tiền. | Giá trị cộng vào ví do server quyết định: crypto lấy giá thị trường nhân số lượng, loại khác lấy `currentValue` mà server đang lưu. `sellPrice` từ client bị bỏ qua. |
| `investments/update` cho người dùng tự đặt `currentValue`, rồi bán theo giá đó. | Chỉ quản trị viên được cập nhật (định giá lại là nghiệp vụ back-office). |
| `loans/create` giải ngân tới 1.000 tỷ, lãi suất 0–100% do client chọn, không giới hạn số khoản vay. | Lãi suất và mức tối đa lấy theo sản phẩm (`Domain/FinancePolicy.cs`). Tổng dư nợ có trần theo KYC (TIER_1: 100 triệu, TIER_2: 2 tỷ). Tối đa 3 khoản vay đang hoạt động. |
| `savings/create` dùng lãi suất client gửi (tới 100%). | Lãi suất lấy theo bảng kỳ hạn của server. |
| `SentToday` cộng `Math.Abs(amount)`, nên tiền **nhận** cũng bị tính vào hạn mức **chuyển**. | Chỉ cộng tiền đi ra (`amount < 0`). Tiền nạp vào dùng hàm riêng `ReceivedToday`. |
| `bank-transfer` không kiểm tra hạn mức ngày. | Dùng chung `RequireTransferLimit` với chuyển nội bộ. |
| Mua crypto trừ ví trước rồi mới lấy giá. | Lấy giá trước; nguồn giá lỗi thì trả 503 mà ví không đổi. |

## 2. Callback VNPay

- IPN thành công đến sau `expires_at` **không cộng tiền**. Đơn chuyển sang `expired`, có cờ `reconciliation_required` để đối soát hoặc hoàn tiền thủ công, và trả `RspCode 02`.
- Giữ nguyên các kiểm tra đang có: chữ ký HMAC-SHA512, merchant, số tiền, đơn đang `pending`, IPN lặp trả 02, Return URL chỉ đọc.

## 3. Idempotency và bảo mật

- `Idempotency-Key` **bắt buộc** cho mọi POST/PUT tài chính. Thiếu key trả 400 `IDEMPOTENCY_KEY_REQUIRED`. Frontend (`transport.ts`) vốn đã luôn gửi key.
- Fingerprint của receipt không còn chứa PIN/mật khẩu. Hash SHA-256 của PIN 6 số có thể dò ngược nếu DB bị lộ.
- `BankingService`: thêm ngoặc cho chuỗi `if/else`. Trước đây `DELETE` ngân hàng không xóa gì mà vẫn trả success, còn `PUT` thiếu một trường bất kỳ thì trả 404.
- `bank-accounts/search` chỉ dành cho admin, để chặn dò tên và số tài khoản của người khác. `verify` chỉ trả các trường cần để xác nhận người nhận.
- Gọi Binance ở `PrefetchAsync`, **trước** khi controller mở transaction ghi và giữ advisory lock toàn cục.
- Swagger chỉ bật ở môi trường Development.

## 4. Typed validation

`Contracts/TypedRequests.cs` có `TransferRequest`, `VnpayCreateRequest`, `SavingsCreateRequest` và `LoanCreateRequest`:
- DataAnnotations (`Required`, `Range`, `StringLength`, `RegularExpression`) cộng `IValidatableObject` cho ràng buộc chéo: đúng một người nhận, VND nguyên, PIN 6 số, giới hạn của sản phẩm vay.
- `WalletServiceBase.Bind<T>()` deserialize body vào lớp typed và chạy `Validator.TryValidateObject`.
- Lỗi trả 400 `VALIDATION_ERROR` kèm `errors` theo từng field, cùng dạng với `ValidationProblemDetails`.

Route catch-all và JSON của frontend giữ nguyên, kể cả các alias như `to_phone`/`receiverPhone` và `pin`/`pinCode`. Các endpoint còn lại vẫn dùng `JsonObject`; đây là phạm vi chuyển đổi tiếp theo.

## 5. Dữ liệu lớn

- `IWalletRepository.Where(table, field, value)`: PostgreSQL lọc bằng SQL, dùng index `ix_htd_transaction_wallet`. Lịch sử giao dịch và hạn mức ngày không còn tải toàn bảng `transactions`.
- `Next()` (MAX+1) tính bằng SQL; dòng mới được ghi khi Commit mà không cần đọc toàn bảng. Bảng `transactions` có thêm index số cho `trans_id`.
- Các trang admin thống kê (`AdministrationService`) vẫn đọc toàn bảng. Đây là giới hạn còn lại.

Phép đo: `scripts/wallet-scale-benchmark.py`. Kết quả nằm trong mục 6.

## 6. Kết quả kiểm thử (22/10/2026, máy Linux, PostgreSQL 16 local, .NET 10.0.113)

Xem `docs/evidence/` (đã commit, kèm `SHA256SUMS`).

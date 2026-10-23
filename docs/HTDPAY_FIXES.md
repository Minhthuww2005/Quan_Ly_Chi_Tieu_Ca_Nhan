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

Bằng chứng: `docs/htdpay-evidence/2026-10-22/` (đã commit cùng `MANIFEST.md` và `SHA256SUMS`, source `81e9b1f`).

| Bộ kiểm thử | Trước sửa | Sau sửa |
|---|---|---|
| `WalletMoneyFlowTests` (mới) | — | 40/40 |
| HTTP smoke + extended (PostgreSQL) | 137 | 144/144 (thêm 7 ca cho hợp đồng mới) |
| `WalletBoundaryLabs` / `WalletProviderTests` / transport | 17 / 15 / 4 | 17 / 15 / 4 |

Một test (`SentToday`) đã được thử đột biến: đưa lại logic cũ thì test báo FAILED. Như vậy test thực sự bắt được lỗi.

**Đo theo kích thước bảng `transactions`** (median ms, 20 mẫu mỗi ô, cùng một database):

| Số dòng | Lịch sử (cũ → mới) | Daily summary (cũ → mới) | Chuyển tiền (cũ → mới) |
|---:|---:|---:|---:|
| 212 | 12,4 → 3,0 | 9,6 → 2,8 | 306 → 196 |
| 10.292 | 98,5 → 2,9 | 84,8 → 2,0 | 306 → 194 |
| 100.372 | 921 → 3,8 | 857 → 2,6 | 1.356 → 196 |
| 300.452 | 2.711 → 3,0 | 2.666 → 2,1 | 3.971 → 193 |

Bản cũ tăng tuyến tính theo số dòng, vì mỗi request tải và parse toàn bộ bảng. Bản mới gần như không đổi.
Chuyển tiền của bản mới khoảng 195 ms, phần lớn là BCrypt xác minh PIN.

**Tải đồng thời** (`docs/htdpay-evidence/2026-10-23/`, 23/10/2026): 1/10/25/50 lệnh chuyển tiền cùng lúc, 5 lượt mỗi mức,
430/430 thành công, số dư được bảo toàn. Throughput dừng ở khoảng 5 req/s; median tăng từ 191 ms (1 lệnh) lên
5.045 ms (50 lệnh), vì mọi lệnh ghi xếp hàng sau advisory lock toàn cục và bước BCrypt xác minh PIN (~190 ms) nằm trong vùng giữ khóa.

**Kiểm tra PIN trước khóa ghi** (`06ac030`, `docs/htdpay-evidence/2026-10-23-pin/`): BCrypt chạy trong `PrefetchAsync`
trước khi lấy advisory lock; trong transaction chỉ so khớp hash đã lưu. Đo lại trên cùng máy với bản trước: throughput
4.7–4.9 → 9.7–10.3 req/s, P95 ở 50 lệnh đồng thời 10.198 → 5.206 ms. Cùng commit: frontend đọc lãi suất từ
`GET /api/loans/products` và `/api/savings/products`; sổ tiết kiệm linh hoạt tính lãi theo số tháng đã gửi. 45 kiểm tra hồi quy, 146 kiểm tra HTTP.

## 7. Chạy lại

```bash
dotnet run --project scripts/WalletMoneyFlowTests
# API QA cổng 5082 (scripts/start-wallet.ps1 -Local -Port 5082 -TestProviders), rồi:
python scripts/wallet-extended-tests.py
# Đo dữ liệu lớn (chỉ local QA; cần psql và biến PG*):
python scripts/wallet-scale-benchmark.py --host new=http://localhost:5082/api --sizes 0,10000,100000
# Tải đồng thời (chỉ local QA):
python scripts/wallet-concurrency-benchmark.py --base http://localhost:5082/api --levels 1,10,25,50 --rounds 5
```

## 8. Thay đổi hợp đồng API cần biết

- Mọi POST/PUT tài chính phải có header `Idempotency-Key`. `wallet-web` đã gửi sẵn; Postman hoặc script tự viết phải thêm header này.
- Loại vay hợp lệ: Vay tiêu dùng, Vay mua xe, Vay mua nhà, Vay kinh doanh. Trường `interestRate` client gửi lên bị bỏ qua.
- `PUT /investments/update` chỉ dành cho admin. `POST /investments/sell` không còn dùng `sellPrice`.
- `GET /bank-accounts/search` chỉ dành cho admin.
- Lỗi validation có thêm `errors: { field: [message] }`; `message` vẫn là lỗi đầu tiên, nên frontend hiện tại vẫn hiển thị đúng.

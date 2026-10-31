# Hồ sơ bằng chứng HTD PAY

Bản nộp: nhánh `fix/htdpay-money-flow` (cũng là `develop`), commit chứa thư mục này. Mã nguồn được kiểm thử ở bản cuối
là `aea5664` (gộp `1563438` với nhánh deploy `codex/integrate-revenueflow` tại `69186f5`); kết quả nằm ở `2026-10-30`.
GitHub Actions run [41023311548](https://github.com/Minhthuww2005/Quan_Ly_Chi_Tieu_Ca_Nhan/actions/runs/41023311548)
đạt cả sáu job trên `aea5664`.

## Các thư mục

| Thư mục | Commit được đo | Nội dung chính |
|---|---|---|
| `2026-10-22` | `81e9b1f` (so với `bf21d48`) | Hồi quy luồng tiền 40, HTTP 92 và 144, đo theo kích thước bảng (480 mẫu) |
| `2026-10-23` | `81e9b1f` | Chuyển tiền đồng thời 1/10/25/50 lệnh |
| `2026-10-23-pin` | `06ac030` (so với `2b5a585`) | Kiểm tra PIN trước khóa ghi; hồi quy 45, HTTP 94 và 146, tải trước/sau |
| `2026-10-23-data` | `8575b27` (so với `84dcebe`) | Đọc theo dòng, trang quản trị bằng SQL; hồi quy 54, HTTP 94 và 163, 421 mẫu |
| `2026-10-24` | `242c0ee` | Body JSON, lỗi giữa trừ và cộng tiền, giao dịch thất bại, EXPLAIN, health 3 lượt, callback VNPay |
| `2026-10-26` | `4356a4f` | Hồi quy 58, ranh giới 17, HTTP 102 và 190, nhóm 50/38/50 (EF Core, Redis, worker, SignalR) |
| `2026-10-26-before-after` | `bf21d48` và `f8cd0f8` | Một bất biến tiền: FAIL trước sửa, PASS sau sửa |
| `2026-10-30` | `aea5664` (bản cuối) | Hồi quy 63, ranh giới 17, HTTP 102 và 190, nhóm 50/38/50, thời gian mở nhóm khi Redis không chạy trước/sau sửa, RevenueFlow 126 + 4 |

Mỗi thư mục có `MANIFEST.md` (commit, lệnh, môi trường, kết quả) và `SHA256SUMS`. Kiểm tra một thư mục:

```bash
cd docs/htdpay-evidence/2026-10-30 && sha256sum -c SHA256SUMS
```

## Cách đọc số liệu

- Các bộ có phạm vi giao nhau (HTTP extended đã gồm smoke; các bộ nhóm kiểm tra cùng chức năng ở ba tầng), nên không cộng
  thành số ca độc lập.
- Hồi quy luồng tiền và kiểm thử service nhóm chạy service thật với repository bộ nhớ; bộ HTTP và bộ tích hợp chạy API thật
  trên PostgreSQL (và Redis) local dựng mới. Không bộ nào chạy trên Neon hay gọi VNPay, Google thật.
- Callback VNPay dùng fixture có chữ ký HMAC do test tạo; chưa có giao dịch sandbox ngân hàng gọi IPN thật. Khi chạy local, `POST /api/payments/vnpay/confirm-local`
  kiểm tra chữ ký ReturnUrl rồi cộng tiền; endpoint này chỉ bật ở Development với ReturnUrl loopback.
- Mọi phép đo hiệu năng chạy trên một máy Linux 2 vCPU, không phải tải production.

## Chạy lại

Lệnh của từng bộ nằm trong `MANIFEST.md` của thư mục tương ứng và trong `.github/workflows/ci.yml`. Fixture dữ liệu QA:
`scripts/fixtures/wallet-qa-import.json`.

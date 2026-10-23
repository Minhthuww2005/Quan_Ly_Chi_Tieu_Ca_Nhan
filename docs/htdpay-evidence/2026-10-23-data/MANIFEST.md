# Bằng chứng HTD PAY — 23/10/2026 (đọc theo dòng, trang admin bằng SQL, khóa đăng nhập/PIN)

**Source đã kiểm thử:** commit `8575b27` trên nhánh `fix/htdpay-money-flow`. Đo so sánh với bản trước đó `84dcebe`,
chạy trên cùng máy (Linux 2 vCPU, .NET 10.0.113, PostgreSQL 16.15 local), cùng database, lần lượt từng host.
Kiểm tra file: `sha256sum -c SHA256SUMS`.

| File | Kết quả |
|---|---|
| `money-flow-results.json` | 54/54 kiểm tra hồi quy (thêm khóa PIN/đăng nhập, liên kết Google, trang admin) |
| `boundary-results.json` | 17/17 |
| `local-smoke.json`, `extended-smoke.json` | 94 và 163 kiểm tra HTTP trên API 5082 |
| `console.txt` | provider 15/15, transport 4/4, build wallet-web và tóm tắt các bộ trên |
| `scale-benchmark.json`, `scale-benchmark-samples.csv`, `scale-benchmark-console.txt` | `python3 scripts/wallet-scale-benchmark.py --sizes 0,100000,300000 --users-ratio 0.1 --samples 15 --timeout 60` |

Median (ms), 15 mẫu mỗi ô, bản cũ `84dcebe` → bản mới `8575b27`. Dòng tổng hợp là tổng số dòng trong bảng `transactions`;
10% số dòng là người dùng tổng hợp.

| Endpoint | 258 dòng | 100.318 dòng | 300.378 dòng |
|---|---:|---:|---:|
| Lịch sử giao dịch | 2,1 → 2,4 | 61,4 → 2,4 | 248,6 → 2,4 |
| Chuyển tiền | 192,5 → 193,8 | 280,3 → 197,8 | 555,6 → 201,0 |
| Admin thống kê | 4,8 → 2,3 | 812,3 → 13,1 | 3.375,3 → 60,7 |
| Admin người dùng (1 trang) | 6,0 → 3,2 | 2.261,5 → 18,7 | 7.669,7 → 57,4 |
| Admin giao dịch (1 trang) | 10,2 → 5,1 | quá 60 s → 98,2 | không đo (đã quá 60 s ở mức nhỏ hơn) → 373,4 |

Bản cũ đọc toàn bảng rồi ghép người dùng với giao dịch trong bộ nhớ, nên thời gian tăng theo số dòng
(trang giao dịch admin tăng theo tích số người dùng × giao dịch). Bản mới đọc theo dòng (`FindOne`, `Where`)
và tính trang admin bằng SQL có `LIMIT/OFFSET`. Đây là phép đo một máy local, không phải tải production.

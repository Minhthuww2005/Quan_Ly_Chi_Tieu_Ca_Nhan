# Tải đồng thời HTD PAY — 23/10/2026

**Source đã đo:** commit `81e9b1f` (code không đổi trong `b86121a`), nhánh `fix/htdpay-money-flow`.
Kiểm tra file bằng `sha256sum -c SHA256SUMS`.

**Lệnh:** `python3 scripts/wallet-concurrency-benchmark.py --base http://localhost:5082/api --levels 1,10,25,50 --rounds 5`

**Cách đo:** mỗi mức gửi đồng thời N lệnh chuyển tiền (rào chắn luồng), lặp 5 lượt. Mỗi người dùng ảo có ví gửi
riêng và địa chỉ loopback riêng (127.0.x.y) như các client khác nhau, nên giới hạn 120 request/phút/IP không chi phối kết quả.
API QA cổng 5082 trên PostgreSQL 16 local, một máy Linux 2 vCPU, .NET 10.0.113.

| Đồng thời | Thành công | Throughput (req/s) | Median (ms) | P95 (ms) |
|---:|---:|---:|---:|---:|
| 1 | 5/5 | 4.7 | 191.3 | 290.8 |
| 10 | 50/50 | 5.1 | 1070.4 | 1969.3 |
| 25 | 125/125 | 5.2 | 2507.9 | 4647.5 |
| 50 | 250/250 | 5.0 | 5044.5 | 9722.4 |

Bảo toàn số dư: tổng số dư các ví đo không đổi; mỗi ví gửi bị trừ đúng (số lệnh thành công × 10.000 VND).

Throughput dừng ở khoảng 5 req/s vì mọi lệnh ghi xếp hàng sau advisory lock toàn cục, và bước BCrypt xác minh PIN
(khoảng 190 ms) đang nằm trong vùng giữ khóa. Đây là phép đo một máy local, không phải tải production.

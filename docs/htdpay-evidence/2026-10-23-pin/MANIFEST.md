# Bằng chứng HTD PAY — 23/10/2026 (kiểm tra PIN trước khóa ghi)

**Source đã kiểm thử:** commit `06ac030` trên nhánh `fix/htdpay-money-flow`. So sánh tải dùng bản trước đó `2b5a585`,
chạy trên cùng máy (Linux 2 vCPU, .NET 10.0.113, PostgreSQL 16 local dựng mới), cùng database, ngay sau nhau.
Kiểm tra file: `sha256sum -c SHA256SUMS`.

| File | Kết quả |
|---|---|
| `money-flow-results.json` | 45/45 kiểm tra hồi quy (service thật, repository bộ nhớ) |
| `boundary-results.json` | 17/17 |
| `local-smoke.json`, `extended-smoke.json` | 94 và 146 kiểm tra HTTP trên API 5082 |
| `console.txt` | provider 15/15, transport 4/4 và tóm tắt các bộ trên |
| `concurrency-before*.{json,csv}` / `concurrency-after*.{json,csv}` | `python3 scripts/wallet-concurrency-benchmark.py --levels 1,10,25,50 --rounds 5` |

| Đồng thời | Throughput trước → sau (req/s) | Median trước → sau (ms) | P95 trước → sau (ms) |
|---:|---:|---:|---:|
| 1 | 4.4 → 5.3 | 223.3 → 187.7 | 279.1 → 193.8 |
| 10 | 4.9 → 9.9 | 1118.8 → 892.4 | 2050.4 → 1008.1 |
| 25 | 4.8 → 10.3 | 2718.8 → 2308.3 | 5037.2 → 2469.0 |
| 50 | 4.7 → 9.7 | 5318.0 → 4798.9 | 10197.6 → 5205.6 |

Cả hai bản: 430/430 lệnh thành công, tổng số dư không đổi, mỗi ví gửi bị trừ đúng số lệnh thành công.
Throughput tăng khoảng 2 lần vì BCrypt không còn chạy trong vùng giữ khóa; giới hạn mới là CPU (2 vCPU chạy BCrypt
song song), nên median ở mức 50 lệnh chỉ giảm ít còn P95 giảm khoảng một nửa. Đây là phép đo một máy local, không phải tải production.

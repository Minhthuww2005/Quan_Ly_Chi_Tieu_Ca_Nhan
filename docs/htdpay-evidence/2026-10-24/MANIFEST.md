# Bằng chứng HTD PAY — 24/10/2026 (body JSON, lỗi giữa trừ và cộng tiền, giao dịch thất bại, EXPLAIN, health lặp)

**Source đã kiểm thử:** mã nguồn trong cùng commit với thư mục này, nhánh `fix/htdpay-money-flow` (cha: `950cd7d`).
Riêng health benchmark được chạy trên `950cd7d`; đường `/api/health` không đổi giữa hai commit.
Kiểm tra file: `sha256sum -c SHA256SUMS`.

**Môi trường:** Linux x64 2 vCPU, .NET SDK 10.0.113, PostgreSQL 16.15 local (cổng 55432, schema `htdpay` dựng mới từ
`scripts/fixtures/wallet-qa-import.json`), Python 3.13.16, Node 22.22.0. Không dùng Neon. Không gọi VNPay hay Google thật.

| File | Lệnh | Kết quả |
|---|---|---|
| `money-flow-results.json` | `dotnet run --project scripts/WalletMoneyFlowTests -- <file>` | 58/58 (thêm: bắt buộc PIN khi mua, gia hạn bảo hiểm, mở sổ tiết kiệm, trả nợ vay; giao dịch bị từ chối được ghi `failed` và không tính vào hạn mức ngày) |
| `boundary-results.json` | `dotnet run --project scripts/WalletBoundaryLabs -- <file>` | 17/17 |
| `local-smoke.json`, `extended-smoke.json` | `WALLET_TEST_BASE=http://localhost:5082/api python3 scripts/wallet-extended-tests.py` | 102 và 190 kiểm tra HTTP (190 đã gồm 102) |
| `callback-state.txt` | log API 5082 của các route `/api/payments/*` và truy vấn SQL ghi trong file | 3 đơn VNPay: 2 success, mỗi đơn đúng 1 dòng giao dịch; 1 failed, 0 dòng |
| `console.txt` | các lệnh trên, `node scripts/wallet-transport-tests.cjs`, `pnpm build` trong `wallet-web` | provider 15/15, transport 4/4, build wallet-web |
| `health-benchmark-run{1,2,3}.json`, `health-benchmark-run{1,2,3}-samples.csv` | `python3 scripts/wallet-health-benchmark.py`, ba lượt liên tiếp | 200 mẫu mỗi mức (1 và 20 worker) mỗi lượt |
| `explain-100000/`, `explain-300000/` | `python3 scripts/wallet-explain.py --rows <N> --users-ratio 0.1 --repeat 5 --out <thư mục>` | kế hoạch đầy đủ của 6 câu SQL, `explain-summary.json` ghi median 5 lần |

Ca mới trong bộ HTTP extended:

- Body JSON đúng cú pháp nhưng không phải object (`[1,2]`, `"abc"`, `5`) → 400 `BODY_NOT_OBJECT`; `null` → 400;
  amount `100.5`, `99999999999999999`, `-5` hoặc thiếu amount → 400 kèm `errors.amount`; số dư không đổi.
- Lỗi giữa trừ và cộng tiền: ví nhận bị khóa bằng `WalletDatabaseTool --local-test-wallet-status` (chỉ tài khoản QA),
  `Credit` ném 403 "Ví không hoạt động" sau khi `Debit` đã trừ ví gửi trong bộ nhớ. Sau request, số dư hai ví không đổi,
  không có dòng chuyển tiền thành công, chỉ có một dòng `failed` với `failure_reason`.

Median Execution Time (ms) của EXPLAIN (ANALYZE, BUFFERS), 5 lần mỗi ô:

| Câu SQL | Kế hoạch chính | 100.556 dòng | 300.556 dòng |
|---|---|---:|---:|
| Lịch sử theo ví (`Where`) | Bitmap Index Scan `ix_htd_transaction_wallet` | 0,224 | 0,558 |
| Đăng nhập theo email (`FindOne`) | Index Scan `ux_htd_email` | 0,053 | 0,068 |
| `Next(transactions, trans_id)` | Index Scan Backward `ix_htd_transaction_number` | 0,131 | 0,123 |
| Admin giao dịch, một trang | Index Scan Backward `ix_htd_transaction_time` | 0,426 | 0,287 |
| Admin giao dịch, thống kê | Parallel Seq Scan `transactions` | 100,7 | 288,3 |
| Admin người dùng, một trang | Seq Scan `users`, sắp xếp toàn bảng | 16,2 | 48,5 |

Health benchmark (200 mẫu mỗi dòng):

| Lượt | Worker | Toàn lượt (s) | Request/s | Mean | Median | P95 | SD |
|---|---:|---:|---:|---:|---:|---:|---:|
| 1 | 1 | 0,269 | 742,4 | 1,31 | 1,26 | 1,60 | 0,34 |
| 1 | 20 | 0,132 | 1518,6 | 12,04 | 9,53 | 24,49 | 7,73 |
| 2 | 1 | 0,331 | 604,7 | 1,61 | 1,53 | 2,59 | 0,43 |
| 2 | 20 | 0,119 | 1674,8 | 11,27 | 7,93 | 38,34 | 10,34 |
| 3 | 1 | 0,188 | 1061,7 | 0,91 | 0,76 | 1,61 | 0,47 |
| 3 | 20 | 0,096 | 2091,0 | 7,01 | 5,99 | 14,97 | 4,64 |

Đây là phép đo một máy local, không phải tải production. EXPLAIN ANALYZE thêm chi phí đo cho từng node, nên số liệu
dùng để so sánh giữa các câu và giữa hai kích thước bảng, không cộng thẳng vào thời gian HTTP.

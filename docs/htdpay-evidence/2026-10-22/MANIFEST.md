# Bằng chứng kiểm thử HTD PAY — 22/10/2026

**Source đã kiểm thử:** commit `81e9b1f` trên nhánh `fix/htdpay-money-flow` (cha: `bf21d48`, `develop`).
Mọi file trong thư mục này được tạo từ đúng commit đó. `SHA256SUMS` dùng để kiểm tra file không bị sửa sau khi tạo:
`sha256sum -c SHA256SUMS`.

**Môi trường:** Linux x64, .NET SDK 10.0.113, PostgreSQL 16 (local, cổng 55432, database dựng mới từ
`scripts/fixtures/wallet-qa-import.json`), Python 3.13, Node 22, pnpm 11.25.0. Không dùng Neon. Không gọi VNPay hay Google thật.

| File | Lệnh | Kết quả |
|---|---|---|
| `money-flow-results.json` | `dotnet run --project scripts/WalletMoneyFlowTests -- <file>` | 40/40 đạt (service thật + repository bộ nhớ) |
| `boundary-results.json` | `dotnet run --project scripts/WalletBoundaryLabs -- <file>` | 17/17 đạt |
| `console.txt` | provider, transport, HTTP, `pnpm build` | 15/15 provider; 4/4 transport; 92 smoke; 144 HTTP; wallet-web build OK |
| `local-smoke.json`, `extended-smoke.json` | `python3 scripts/wallet-extended-tests.py` (API cổng 5082) | 92 và 144 kiểm tra HTTP đạt |
| `scale-benchmark.json`, `scale-benchmark-samples.csv` | `python3 scripts/wallet-scale-benchmark.py --host new=… --host old=… --sizes 0,10000,100000,300000 --samples 20` | 480 mẫu thô; `old` = `bf21d48`, `new` = `81e9b1f` |

Giới hạn: số đo là thời gian phía client của request tuần tự trên một máy, không phải tải production hay SLA.
Thời gian chuyển tiền của bản mới (~195 ms) chủ yếu là BCrypt xác minh PIN, nên gần như không đổi theo kích thước bảng.
Các bảng admin (thống kê, KYC) vẫn đọc toàn bảng và chưa được đo ở đây.

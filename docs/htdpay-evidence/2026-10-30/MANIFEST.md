# Bằng chứng HTD PAY — 30/10/2026 (bản cuối aea5664)

**Source đã kiểm thử:** commit `aea5664` trên nhánh `fix/htdpay-money-flow` (cũng là `develop`). Commit này gộp
`1563438` (sửa trang Nhóm khi Redis không chạy) với nhánh `codex/integrate-revenueflow` tại `69186f5` của Thanh
(`924145b`: cấu hình deploy Vercel/Render, xác nhận VNPay khi chạy local). Cây code giống hệt `69186f5`.
GitHub Actions run 41023311548 trên `fix/htdpay-money-flow` đạt cả sáu job.
Kiểm tra file: `sha256sum -c SHA256SUMS`.

**Môi trường:** Linux x64 2 vCPU, .NET SDK 10.0.114, PostgreSQL 16.15 local (cổng 55432, schema `htdpay` dựng mới từ
`scripts/fixtures/wallet-qa-import.json`), Redis 7.0.15 local, Python 3.13.18, Node 22.23.4. Không dùng Neon.
Không gọi VNPay hay Google thật.

| File | Lệnh | Kết quả |
|---|---|---|
| `money-flow-results.json` | `dotnet run --project scripts/WalletMoneyFlowTests -- <file>` | 63/63 (thêm 5 ca xác nhận VNPay local: đúng chủ đơn, sai chữ ký, chỉ cộng một lần, tắt ở Production và với ReturnUrl công khai) |
| `boundary-results.json` | `dotnet run --project scripts/WalletBoundaryLabs -- <file>` | 17/17 |
| `local-smoke.json`, `extended-smoke.json` | `WALLET_TEST_BASE=http://localhost:5082/api python3 scripts/wallet-extended-tests.py` | 102 và 190 kiểm tra HTTP (190 đã gồm 102) |
| `group-http-results.json` | `python3 scripts/wallet-group-http-tests.py --local-admin-container htdpay-groups-qa-15439` | 38 kiểm tra HTTP nhóm; sửa cùng version trả 200 và 409; số dư ví không đổi |
| `group-integration-results.json` | `dotnet run --project scripts/WalletGroupIntegration` (API cổng 5083, database riêng `htdpay_groups_qa`) | 50 kiểm tra: EF Core và rollback chung, Redis MISS/HIT, outbox worker, SignalR WebSocket |
| `group-timing-before-924145b.json`, `group-timing-after-aea5664.json` | `python3 scripts/wallet-group-timing.py --label <commit> --out <file>`, Redis trỏ tới cổng không có dịch vụ | Mở chi tiết nhóm: 11131, 5962, 5982, 5993 ms trước sửa; 28, 10, 8, 7 ms sau sửa |
| `console.txt` | các lệnh trên, `WalletProviderTests`, `WalletGroupTests`, `node scripts/wallet-transport-tests.cjs`, `pnpm build`, `dotnet test RevenueManagement.slnx` | provider 15/15, service nhóm 50/50, transport 4/4, build wallet-web, RevenueFlow 126 + 4 |

Ghi chú khi chạy:

- `wallet-group-http-tests.py` cấp quyền admin bằng `docker exec <container> psql`; máy đo không có Docker nên câu SQL đó
  được chạy bằng `psql` trên cùng database local qua một script thay lệnh `docker`.
- Bộ HTTP nhóm được chạy sau bộ HTTP ví hơn 60 giây vì rate limiter 120 request/phút theo IP.
- Trong `group-timing-*`, hai mẫu cuối (cách nhau 1 giây) của bản trước sửa nhanh vì bản đó bỏ qua Redis trong 5 giây sau
  một lần lỗi; sau 5 giây, mỗi lần mở lại phải chờ hết timeout.
- Các bộ giao nhau, không cộng thành số ca độc lập. Đây là kết quả local; kết quả CI là run 41023311548.

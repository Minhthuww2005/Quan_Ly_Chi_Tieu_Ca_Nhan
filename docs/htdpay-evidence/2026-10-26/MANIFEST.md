# Bằng chứng HTD PAY — 26/10/2026 (bản cuối: ví cá nhân + chi tiêu nhóm)

**Source đã kiểm thử:** commit `4356a4f` trên nhánh `fix/htdpay-money-flow` — gộp `242c0ee` (sửa PIN, giao dịch thất bại,
body JSON) với `develop` tại `197c033` (chi tiêu nhóm dùng EF Core, Redis, outbox worker, SignalR).
Kiểm tra file: `sha256sum -c SHA256SUMS`.

**Môi trường:** Linux x64 2 vCPU, .NET SDK 10.0.113, PostgreSQL 16.15 local (cổng 55432), Redis local (cổng 6379),
Python 3.13.16, Node 22.22.0, pnpm 11.25.0. Không dùng Neon. Không gọi VNPay hay Google thật.

| File | Lệnh | Kết quả |
|---|---|---|
| `money-flow-results.json` | `dotnet run --project scripts/WalletMoneyFlowTests -- <file>` | 58/58 (service thật, repository bộ nhớ) |
| `boundary-results.json` | `dotnet run --project scripts/WalletBoundaryLabs -- <file>` | 17/17 |
| `local-smoke.json`, `extended-smoke.json` | `WALLET_TEST_BASE=http://localhost:5082/api python3 scripts/wallet-extended-tests.py` | 102 và 190 kiểm tra HTTP (190 đã gồm 102); schema `htdpay` dựng mới |
| `group-http-results.json` | `python3 scripts/wallet-group-http-tests.py --local-admin-container htdpay-groups-qa-15439` | 38 kiểm tra HTTP nhóm; hai lệnh sửa cùng version trả 200 và 409; số dư ví không đổi |
| `group-integration-results.json` | `dotnet run --project scripts/WalletGroupIntegration` (API thứ hai cổng 5083, database riêng `htdpay_groups_qa`) | 50 kiểm tra: EF Core JSONB và rollback chung, Redis MISS/HIT, outbox worker, SignalR WebSocket |
| `console.txt` | các lệnh trên, `WalletProviderTests`, `WalletGroupTests`, `node scripts/wallet-transport-tests.cjs`, `pnpm build` | provider 15/15, service nhóm 50/50, transport 4/4, build wallet-web |

Ghi chú khi chạy:

- `wallet-group-http-tests.py` cấp quyền admin cho tài khoản QA bằng `docker exec <container> psql`. Máy đo không có
  Docker, nên câu SQL đó được chạy bằng `psql` trên cùng database local qua một script thay thế lệnh `docker`.
- Bộ HTTP nhóm được chạy sau bộ HTTP ví hơn 60 giây, vì rate limiter 120 request/phút theo IP trả 503 khi hai bộ chạy
  liền nhau từ cùng địa chỉ.
- Các bộ có phạm vi giao nhau, không cộng thành số ca độc lập. Đây là kết quả local, không phải một lần chạy GitHub Actions.

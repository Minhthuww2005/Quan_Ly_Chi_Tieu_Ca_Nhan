# REV-02 — Cache & Query

Phạm vi của nhánh `thanh/cache-query` bám đúng REV-02 và mục 3.1 trong workbook:

- Một endpoint `GET /api/reports/revenue`.
- Lọc theo `groupId`, `from`, `to`, tùy chọn `walletId`.
- `groupBy` nhận một trong `date`, `wallet`, `category`, `member`.
- Dữ liệu được tổng hợp trực tiếp từ PostgreSQL bằng LINQ; không có bảng report và không có dữ liệu giả.
- Redis cache có TTL, key đủ chiều lọc, log `HIT/MISS`, và invalidation theo Group.

## Chạy local

Không commit mật khẩu Neon. Đặt secret bằng User Secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:Database" "Host=YOUR_NEON_HOST;Port=5432;Database=neondb;Username=YOUR_ROLE;Password=YOUR_PASSWORD;SSL Mode=Require;Channel Binding=Require" --project src/RevenueManagement.Api
```

Chạy Redis và API:

```powershell
docker compose up -d redis
dotnet run --project src/RevenueManagement.Api
```

JWT do nhánh `duy/core-auth` cung cấp phải có claim `sub`, `userId` hoặc `ClaimTypes.NameIdentifier` là khóa `long` thật của user. Service luôn kiểm tra user còn hiệu lực trong `GroupMembers`; truyền `groupId` của nhóm khác sẽ bị từ chối.

Ví dụ gọi API sau khi Auth được merge:

```http
GET /api/reports/revenue?groupId=<GROUP_ID>&from=2026-01-01&to=2026-01-31&groupBy=category
Authorization: Bearer <JWT>
```

Header phản hồi `X-Report-Cache` là `MISS` ở lần đầu và `HIT` ở lần sau khi cùng bộ lọc. TTL lấy từ `Redis:ReportTtlSeconds`.

## Contract invalidation cho Revenue CRUD

Sau khi transaction của REV-01 commit thành công, nhánh A resolve `IRevenueReportCacheInvalidator` và gọi:

```csharp
await cacheInvalidator.InvalidateGroupAsync(groupId, cancellationToken);
```

Invalidation đổi version của Group nên toàn bộ key cũ hết hiệu lực mà không cần quét wildcard Redis. Không được gọi trước khi database commit.

## Schema phụ thuộc nhánh Core

`ReportingDbContext` là read-only và đọc các bảng do migration của nhánh A sở hữu: `transactions`, `wallets`, `categories`, `group_members`. Mapping dùng đúng tên cột snake_case trong migration hiện tại của `duy/core-auth`; module không tạo bảng report riêng.

Các index cần có trong migration của A:

- `Wallets(GroupId)` để thu hẹp đúng Group.
- `RevenueTransactions(WalletId, OccurredOn)`.
- `RevenueTransactions(CategoryId, OccurredOn)`.

## Benchmark thật

Chỉ ghi số sau khi database đã có dữ liệu kiểm thử và Redis đang chạy. Với cùng URL và JWT:

1. Invalidate Group.
2. Gọi một lần để đo `MISS`.
3. Gọi lặp lại để đo `HIT`.
4. Lưu average, P95, số truy vấn database và số mẫu.
5. Sửa một Revenue, commit, gọi invalidation và chứng minh lần kế tiếp quay lại `MISS`.

Repo không chứa số benchmark dựng sẵn vì workbook yêu cầu phép đo thật.

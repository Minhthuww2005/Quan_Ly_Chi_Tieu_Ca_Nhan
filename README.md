# RevenueFlow — Cache & Query (REV-02)

README này mô tả mã hiện có trên nhánh **`thanh/cache-query`**, phụ trách báo cáo doanh thu, truy vấn PostgreSQL và Redis cache. Dự án dùng ASP.NET Core / EF Core .NET 10; giao diện React + Vite và các module khác vẫn nằm trong bộ khung chung của nhóm.

## Chức năng đã triển khai

- `GET /api/reports/revenue`: tổng hợp doanh thu từ PostgreSQL, lọc theo `groupId`, `from`, `to` và tùy chọn `walletId`.
- Nhóm kết quả theo `groupBy=date|wallet|category|member`.
- Kiểm tra người dùng thuộc nhóm trước khi trả báo cáo, kể cả khi cache HIT.
- Redis cache có key theo bộ lọc, TTL và invalidation theo version của Group.
- Header `X-Report-Cache` (`HIT`/`MISS`), `X-Database-Query-Count` và `X-Request-ID` hỗ trợ kiểm tra cache, truy vấn và đối chiếu log.
- Health check cho kết nối database và thao tác ghi/đọc/xóa trên Redis.
- Unit test cho cache key; integration test cho dữ liệu thật, invalidation và benchmark Average/P95.

## Phần còn phụ thuộc các nhánh khác

`Program.cs` chưa đăng ký hệ thống xác thực JWT của nhánh `duy/core-auth`. Vì vậy, chỉ chạy API trên nhánh này chưa đủ để gọi báo cáo bằng Bearer token: request không có user trong context sẽ nhận `401`. Sau khi ghép Auth, JWT cần có claim `sub`, `userId` hoặc `ClaimTypes.NameIdentifier` chứa ID kiểu `long` của user.

`ReportingDbContext` đọc các bảng `transactions`, `wallets`, `categories`, `group_members` theo schema của Core. Database cần có schema và dữ liệu phù hợp; module báo cáo không tạo bảng report riêng. Revenue CRUD cần gọi invalidation sau khi commit thành công. Các module Auth, Revenues, Recurring, Realtime và giao diện chưa được xem là hoàn chỉnh chỉ vì đã có thư mục trong repo.

## Chạy local

Yêu cầu: .NET 10 SDK, Docker có Docker Compose và PostgreSQL/Neon đã có schema phù hợp.

```powershell
git switch thanh/cache-query
dotnet restore src/RevenueManagement.Api
docker compose up -d redis
```

Cấu hình database bằng User Secrets, thay các placeholder bằng thông tin của môi trường local:

```powershell
dotnet user-secrets set "ConnectionStrings:Database" "Host=YOUR_NEON_HOST;Port=5432;Database=neondb;Username=YOUR_ROLE;Password=YOUR_PASSWORD;SSL Mode=Require;Channel Binding=Require" --project src/RevenueManagement.Api
dotnet run --project src/RevenueManagement.Api
```

Không commit connection string thật vào Git. Khi chạy bằng environment variable, dùng `ConnectionStrings__Database`. Redis local mặc định là `localhost:6379`; có thể đổi qua `Redis__ConnectionString`. `Redis:ReportTtlSeconds` mặc định 60 giây, chấp nhận từ 5 đến 3600 giây.

Kiểm tra dịch vụ:

- `http://localhost:5080/health/database`: kiểm tra kết nối PostgreSQL.
- `http://localhost:5080/health/redis`: kiểm tra ghi, đọc và xóa key trên Redis.

### Gọi báo cáo sau khi ghép Auth

```http
GET /api/reports/revenue?groupId=<GROUP_ID>&from=2026-09-01&to=2026-10-31&groupBy=category
Authorization: Bearer <JWT>
```

Với cùng bộ lọc, lần đầu trả `X-Report-Cache: MISS`, lần tiếp theo trả `HIT` nếu cache còn hiệu lực. Người dùng không thuộc nhóm nhận `403`; bộ lọc không hợp lệ nhận `400`.

### Invalidation sau Revenue CRUD

Sau khi transaction của REV-01 commit thành công, resolve `IRevenueReportCacheInvalidator` và gọi:

```csharp
await cacheInvalidator.InvalidateGroupAsync(groupId, cancellationToken);
```

Version của Group thay đổi làm các key cũ hết hiệu lực mà không cần quét wildcard Redis. Hướng dẫn contract và schema chi tiết nằm trong [Cache & Query](docs/CACHE_QUERY.md).

## Kiểm thử và benchmark

Chạy unit test cache key:

```powershell
dotnet test tests/RevenueManagement.UnitTests --filter FullyQualifiedName~RevenueReportCacheKeyTests
```

Integration test cần environment variable riêng; cấu hình User Secrets cho API không tự cung cấp các biến này cho test:

```powershell
$env:ConnectionStrings__Database = "<POSTGRESQL_CONNECTION_STRING>"
$env:Redis__ConnectionString = "localhost:6379"
dotnet test tests/RevenueManagement.IntegrationTests --filter FullyQualifiedName~RevenueReportIntegrationTests --logger "console;verbosity=detailed"
```

Database kiểm thử cần có thành viên nhóm và doanh thu trong khoảng 01/09/2026–31/10/2026. Test đọc dữ liệu thật và thay đổi version cache của nhóm được chọn. Nếu thiếu một trong hai biến môi trường, các test này trả về ngay và **không thực hiện kiểm tra tích hợp hay benchmark**.

- `Report_UsesNeonAndRedis_WithRealInvalidation`: kiểm tra `MISS → HIT → invalidate → MISS`, tổng doanh thu và số truy vấn.
- `Benchmark_ReportsRealAverageP95AndQueryCount`: đo 10 mẫu MISS, 30 mẫu HIT và in dòng `REAL_BENCHMARK`.

Kết quả được ghi lại ngày 04/10/2026 trong [tài liệu Cache & Query](docs/CACHE_QUERY.md):

| Trạng thái | Số mẫu | Average | P95 | Truy vấn PostgreSQL/request |
|---|---:|---:|---:|---:|
| MISS | 10 | 725.76 ms | 984.78 ms | 3 |
| HIT | 30 | 232.94 ms | 241.71 ms | 1 |

Đây là kết quả đã lưu của lần đo trước, không phải cam kết hiệu năng hay kết quả chạy lại khi sửa README. Cache HIT vẫn truy vấn membership để kiểm tra quyền; thời gian phụ thuộc mạng và trạng thái Neon.

## Cấu trúc liên quan đến nhánh

```text
src/RevenueManagement.Api/
├─ Features/Reports/              # Controller, query, DTO, read-only DbContext, DI, log
├─ Infrastructure/Caching/        # Redis cache, invalidation, health check
└─ Program.cs                     # Đăng ký Reports và route health check

tests/
├─ RevenueManagement.UnitTests/RevenueReportCacheKeyTests.cs
└─ RevenueManagement.IntegrationTests/RevenueReportIntegrationTests.cs

docs/CACHE_QUERY.md               # Contract, schema và benchmark
Dockerfile
docker-compose.yml
```

## Tài liệu nhóm

- [Phân công chính thức](docs/ASSIGNMENTS.md)
- [Phạm vi cố định](docs/SCOPE.md)
- [Ma trận yêu cầu](docs/REQUIREMENT_MATRIX.md)
- [Quy trình nhóm](docs/TEAM_WORKFLOW.md)
- [Kiến trúc](docs/ARCHITECTURE.md)
- [Khai báo AI](docs/AI_USAGE.md)

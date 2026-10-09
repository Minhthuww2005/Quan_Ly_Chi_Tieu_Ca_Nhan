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

## Nhánh `thuw/queue-background` — REV-03 Doanh thu định kỳ, Queue & Background (Thuw)

Trạng thái: đã chạy với Neon PostgreSQL thật; build 0 lỗi 0 cảnh báo; unit test 91/91 pass.
Nhánh được xây trên `duy/core-auth` (entity, `AppDbContext`, migration, JWT) vì module cần các contract đó để biên dịch.
Tài liệu chi tiết: [docs/QUEUE_BACKGROUND.md](docs/QUEUE_BACKGROUND.md).

### Yêu cầu đã làm

| Mã | Nội dung | Vị trí |
|---|---|---|
| 1B.3 | Binding + validation: DataAnnotations, `IValidatableObject`, `RevenueValidator` dùng chung với REV-01 (Amount > 0, ngày hợp lệ, Wallet/Category cùng Group, Category là Income, user là thành viên) | `Features/Recurring/RecurringRuleDtos.cs`, `RecurringRuleRequestValidator.cs`, `Features/Revenues/RevenueValidator.cs` |
| 1B.4 | `BackgroundService` quét quy tắc đến hạn theo múi giờ của quy tắc, graceful cancellation | `Infrastructure/Workers/RecurringRuleScanner.cs`, `RecurringRevenueWorker.cs` |
| 3.2 | Queue `Channel<T>` bounded, retry exponential backoff + jitter, idempotency `(RuleId, OccurrenceDate)` + `idempotency_key`, RunLog Created/Skipped/Failed | `Infrastructure/Workers/**` |
| REV-03 | CRUD `/api/recurring-rules`, xem nhật ký chạy, trigger thủ công; `CalculateNextRunOn` cho Daily/Weekly/Monthly (một module, Frequency là tham số) | `Features/Recurring/**` |
| 2.4 | Unit test: lịch, validation, queue/backoff, processor (chạy 2 lần = 1 transaction), service (IDOR 404, 409) | `tests/RevenueManagement.UnitTests/**` |
| 2.9 | Mẫu khai báo AI theo module | `docs/AI_USAGE.md` |

### API

| Method | Route | Mô tả |
|---|---|---|
| GET | `/api/recurring-rules?walletId=` | Danh sách quy tắc trong các nhóm user là thành viên |
| GET | `/api/recurring-rules/{id}` | Chi tiết |
| POST | `/api/recurring-rules` | Tạo (201 / 400 / 404) |
| PUT | `/api/recurring-rules/{id}` | Sửa, cần `rowVersion` (409 nếu lệch) |
| PATCH | `/api/recurring-rules/{id}/active` | Bật/tắt; bật lại thì chạy từ hôm nay, không chạy bù |
| DELETE | `/api/recurring-rules/{id}` | Chỉ xóa được khi chưa có nhật ký (409 → hãy tắt) |
| GET | `/api/recurring-rules/{id}/runs?status=&from=&to=&limit=` | Nhật ký Created/Skipped/Failed |
| POST | `/api/recurring-rules/{id}/trigger` | Đưa vào hàng đợi ngay (202) — dùng khi demo |

Tất cả yêu cầu JWT. Quy tắc của nhóm khác luôn trả 404.

### Chạy local

```powershell
# Một lần: cấu hình secret (không commit vào Git)
dotnet user-secrets set "ConnectionStrings:Database" "Host=...;Port=5432;Database=neondb;Username=...;Password=...;SSL Mode=Require;Channel Binding=Require" --project src/RevenueManagement.Api
dotnet user-secrets set "Jwt:SigningKey" "<chuỗi ngẫu nhiên dài ít nhất 32 ký tự>" --project src/RevenueManagement.Api

dotnet build RevenueManagement.slnx
dotnet test tests/RevenueManagement.UnitTests
dotnet run --project src/RevenueManagement.Api      # http://localhost:5080/swagger
```

> **Lưu ý:** khi app chạy, worker sẽ tạo giao dịch thật trên database Neon dùng chung cho các quy tắc đến hạn.
> Chỉ muốn thử API mà không ghi dữ liệu: `$env:Worker__Enabled = "false"` trước khi `dotnet run`.

Cấu hình section `Worker` (override bằng `Worker__<Key>`): `Enabled`, `PollIntervalSeconds`, `MaxRetryAttempts`,
`BaseRetryDelaySeconds`, `MaxRetryDelaySeconds`, `QueueCapacity`, `ScanBatchSize`, `MaxCatchUpOccurrences`.
Sai cấu hình thì app dừng ngay khi khởi động (`ValidateOnStart`).

### Điểm tích hợp cần các thành viên khác review

| File / contract | Thay đổi | Người cần biết |
|---|---|---|
| `Domain/Enums/TransactionKind.cs`, `RecurrenceFrequency.cs` | Đổi sang `Income = 1`, `Daily/Weekly/Monthly = 1/2/3` cho khớp CHECK constraint trên Neon (`kind IN (1,2)`, `frequency IN (1,2,3)`) | Duy (owner), Thanh |
| `Features/Reports/ReportingDbContext.cs` (nhánh Thanh) | Khi merge phải **bỏ** conversion `+1/-1` cho `Kind`, nếu không sẽ lệch hai lần | Thanh |
| `Program.cs` | Thêm 1 dòng `builder.Services.AddRecurringRevenue(builder.Configuration);` | Duy |
| `Options/WorkerOptions.cs` | Điền thuộc tính + DataAnnotations cho section `Worker` | Tuyen |
| `IRevenueChangedPublisher` | Gọi `PublishAsync(groupId)` sau commit; mặc định NoOp — B (cache) và D (SignalR) đăng ký implementation thật | Thanh, Tuyen |
| `RevenueValidator` | `ValidateAsync(RevenueInput, userId)` để `/api/revenues` dùng lại | Duy |
| `recurring_rules.day_of_week` | Theo ISO 1 = Thứ Hai … 7 = Chủ nhật (khớp CHECK 1..7) | Tuyen (form React) |
| `tests/RevenueManagement.UnitTests.csproj` | Thêm `Microsoft.EntityFrameworkCore.InMemory` | Cả nhóm |

## Khởi tạo Git


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


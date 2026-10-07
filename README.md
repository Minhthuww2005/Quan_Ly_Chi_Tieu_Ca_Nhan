# RevenueFlow — skeleton phân chia đồ án

Repo này **chỉ là bộ khung**, chưa có business logic. Cấu trúc bám theo file chức năng của đề tài: 3 nghiệp vụ REV-01/02/03 và các yêu cầu kỹ thuật dùng chung.

## Cây thư mục

```text
src/RevenueManagement.Api/
├─ Common/                         # exception, middleware, error model
├─ Domain/                         # 8 entity + enum theo ERD tối giản
├─ Features/
│  ├─ Auth/                        # JWT + refresh token
│  ├─ Revenues/                    # REV-01, Minimal API CRUD
│  ├─ Reports/                     # REV-02, Controller + query
│  └─ Recurring/                   # REV-03, recurring rules
├─ Infrastructure/
│  ├─ Authorization/               # membership + IDOR protection
│  ├─ Caching/                     # Redis TTL/invalidation
│  ├─ Persistence/                 # EF Core/DbContext/config/migration
│  ├─ Realtime/                    # SignalR room theo Group
│  └─ Workers/                     # Channel queue + BackgroundService
└─ Options/                        # Jwt/Redis/Worker options

web/src/
├─ api/ auth/ components/ hooks/ layouts/
└─ features/{dashboard,revenues,recurring}/

tests/
├─ RevenueManagement.UnitTests/
└─ RevenueManagement.IntegrationTests/
```

## Chia việc 4 người

| Người | Nhánh đề xuất | Folder sở hữu chính | Công việc |
|---|---|---|---|
| Duy — Core & Auth | `duy/core-auth` | `1A.1`, `1A.2`, `1A.4`, `1B.1`, `1B.2` | Middleware, DI, error format, EF Core/migration, JWT/resource authorization |
| Thanh — Cache & Query | `thanh/cache-query` | `1A.3`, `2.2`, `2.7`, `2.8`, `3.1` | Controller/Minimal API, Docker, observability, tài liệu, Redis/query/benchmark |
| Thuw — Queue & Background | `thuw/queue-background` | `1B.3`, `1B.4`, `2.4`, `2.9`, `3.2` | Validation, hosted service, testing, AI declaration, queue/retry/idempotency |
| Tuyen — Realtime & Options | `tuyen/realtime-options` | `1B.5`, `2.3`, `2.5`, `2.6`, `3.3` | Options, config/secret, CI, security/CORS, SignalR/reconnect/resync |

Mỗi người chủ trì 5 yêu cầu. `2.1 Git` là trách nhiệm chung. Với Testing và Documentation, người chủ trì dựng cấu trúc/tổng hợp; từng thành viên vẫn phải viết test và tài liệu cho module mình. React chỉ là lớp giao diện sử dụng REV-01/02/03, không phải chức năng nghiệp vụ thứ tư.

## Thứ tự ghép code để ít conflict

1. A chốt entity, DTO contract và `AppDbContext` trước.
2. B/C/D code theo contract, hạn chế cùng sửa `Program.cs`.
3. Mỗi người tạo extension DI riêng trong module của mình; A chỉ gọi các extension đó ở `Program.cs`.
4. PR nhỏ theo module, bắt buộc người khác review trước merge.
5. Chỉ tạo migration sau khi cả nhóm chốt entity để tránh migration xung đột.

## Quy ước class

- Endpoint/Controller nhận request và gọi service, không chứa business logic.
- Service giữ use case và business rule.
- Entity chỉ thể hiện trạng thái và invariant cốt lõi.
- Infrastructure chứa code phụ thuộc EF Core, Redis, SignalR, JWT, queue.
- DTO frontend/backend phải khớp nhau; không trả thẳng EF entity ra API.
- Không tin `GroupId` hoặc `CreatedByUserId` từ client nếu có thể suy ra từ token/context.

## Kết nối Neon PostgreSQL

Không ghi connection string thật vào `appsettings.json` hoặc Git. Với môi trường local, dùng .NET User Secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:Database" "Host=YOUR_NEON_HOST;Port=5432;Database=neondb;Username=YOUR_ROLE;Password=YOUR_PASSWORD;SSL Mode=Require;Channel Binding=Require" --project src/RevenueManagement.Api
dotnet run --project src/RevenueManagement.Api
```

Kiểm tra kết nối thật tại `http://localhost:5080/health/database`. Kết quả `Healthy` mới được xem là kết nối thành công.

Neon cung cấp URI dạng `postgresql://...`; ứng dụng dùng chuỗi key/value của Npgsql như trong `.env.example`. Khi deploy, đặt secret bằng environment variable `ConnectionStrings__Database`.

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

```powershell
git init
git add .
git commit -m "chore: scaffold revenue management architecture"
```

Sau đó tạo 4 branch như bảng phân công. Chưa chạy migration hay benchmark ở commit skeleton này.

Trước khi bắt đầu code, mỗi thành viên phải đọc:

- [Phân công chính thức](docs/ASSIGNMENTS.md)
- [Phạm vi cố định](docs/SCOPE.md)
- [Ma trận 21 yêu cầu](docs/REQUIREMENT_MATRIX.md)
- [Quy trình nhóm](docs/TEAM_WORKFLOW.md)

Xem thêm [sơ đồ kiến trúc](docs/ARCHITECTURE.md) và [khai báo AI](docs/AI_USAGE.md).

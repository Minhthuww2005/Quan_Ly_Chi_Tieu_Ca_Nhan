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

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
| A — Core & Auth | `feature/core-auth` | `Common`, `Domain`, `Features/Auth`, `Features/Revenues`, `Infrastructure/Authorization`, `Infrastructure/Persistence` | Pipeline, DI, EF Core, JWT, policy theo Group, Revenue Minimal API |
| B — Cache & Query | `feature/report-cache` | `Features/Reports`, `Infrastructure/Caching`, `web/src/features/dashboard` | Report Controller, LINQ aggregate, index, Redis TTL/invalidation, benchmark |
| C — Queue & Background | `feature/recurring-worker` | `Features/Recurring`, `Infrastructure/Workers`, validators, unit tests | Rule định kỳ, queue, worker, retry/backoff, idempotency |
| D — Realtime & Frontend | `feature/realtime-ui` | `Infrastructure/Realtime`, `Options`, `web/src` | SignalR room/reconnect, Options, React shell và tích hợp API |

Phần `.github`, Docker, integration tests, README và minh chứng Tầng 2 là việc chung. Các file placeholder có `TODO(A/B/C/D)` để nhận biết người chủ trì.

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

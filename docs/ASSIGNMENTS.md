# Phân công chính thức theo workbook

Tài liệu này là nguồn phân công duy nhất của nhóm. Không tự thêm chức năng hoặc đổi người phụ trách nếu chưa thống nhất trong pull request.

## A — Core, Auth và Revenue CRUD

Nhánh: `duy/core-auth`

### File được giao

- `src/RevenueManagement.Api/Program.cs`
- `src/RevenueManagement.Api/Common/**`
- `src/RevenueManagement.Api/Domain/**`
- `src/RevenueManagement.Api/Features/Auth/**`
- `src/RevenueManagement.Api/Features/Revenues/RevenueEndpoints.cs`
- `src/RevenueManagement.Api/Features/Revenues/RevenueService.cs`
- `src/RevenueManagement.Api/Features/Revenues/RevenueDtos.cs`
- `src/RevenueManagement.Api/Infrastructure/Authorization/**`
- `src/RevenueManagement.Api/Infrastructure/Persistence/**`

### Phải hoàn thành

- TECH-01: middleware đúng thứ tự, request ID và case cấu hình sai để demo.
- TECH-02: đăng ký DI và giải thích lifetime.
- TECH-03: một format lỗi `code/message/requestId`.
- REV-01: Minimal API CRUD tại `/api/revenues`.
- EF Core mapping và migration cho đúng tám entity đã chốt.
- JWT, refresh token và authorization theo membership của Group.
- IDOR: user Group A không được đọc hoặc sửa dữ liệu Group B.
- Sau khi transaction commit, phát contract `RevenueChanged(GroupId)` cho B và D.

### Không làm

- Không viết query tổng hợp báo cáo hoặc Redis cache.
- Không viết recurring worker.
- Không tự thêm Expense, Budget hoặc BudgetAlert.

### Bàn giao cho người khác

- DTO Revenue đã chốt.
- Schema/migration đã chốt.
- Interface/event sau commit.
- Claims JWT thống nhất là `sub` hoặc `userId`, chỉ chọn một.

## B — Cache & Query

Nhánh: `thanh/cache-query`

### File được giao

- `src/RevenueManagement.Api/Features/Reports/**`
- `src/RevenueManagement.Api/Infrastructure/Caching/**`
- Các index phục vụ report trong `Infrastructure/Persistence/Configurations` phải tạo PR và nhờ A review.
- `web/src/features/dashboard/dashboardApi.ts`

### Phải hoàn thành

- REV-02: `GET /api/reports/revenue` bằng Controller.
- Filter theo thời gian và tùy chọn Wallet.
- `groupBy` chỉ là tham số: date, wallet, category hoặc member.
- LINQ aggregate/projection; không tạo bảng Report riêng.
- Redis cache key có Group, Wallet, period và groupBy.
- TTL, cache hit/miss và invalidation sau `RevenueChanged`.
- Benchmark thật: average, P95 và query count trước/sau cache.

### Không làm

- Không tạo bốn endpoint report riêng cho từng `groupBy`.
- Không tự sửa Revenue CRUD.
- Không điền số benchmark giả.

### Bàn giao cho D

- Contract `RevenueReportResponse`.
- Hàm lấy snapshot mới nhất sau SignalR reconnect.

## C — Queue & Background

Nhánh: `thuw/queue-background`

### File được giao

- `src/RevenueManagement.Api/Features/Revenues/RevenueValidator.cs`
- `src/RevenueManagement.Api/Features/Recurring/**`
- `src/RevenueManagement.Api/Infrastructure/Workers/**`
- `tests/RevenueManagement.UnitTests/RecurringScheduleTests.cs`
- Các test validation liên quan.

### Phải hoàn thành

- Validation: Amount > 0, date hợp lệ, Wallet và Category cùng Group, Category là Income.
- REV-03: một module recurring; Daily/Weekly/Monthly là enum, không phải ba chức năng.
- `CalculateNextRunOn` hoặc phương thức tương đương.
- BackgroundService quét rule đến hạn.
- Queue dùng `Channel<T>` theo lựa chọn của workbook.
- Retry exponential backoff và graceful cancellation.
- Idempotency key duy nhất `(RuleId, OccurrenceDate)`.
- RunLog có Created, Skipped hoặc Failed.
- Chạy cùng occurrence hai lần chỉ tạo một transaction.

### Không làm

- Không thêm RabbitMQ nếu nhóm chưa quyết định thay Channel.
- Không viết dashboard hoặc Redis report cache.
- Không tách Daily/Weekly/Monthly thành module riêng.

### Bàn giao cho A/D

- Gọi luồng tạo Revenue do A cung cấp thay vì copy logic.
- Phát `RevenueChanged(GroupId)` sau commit để D cập nhật dashboard.

## D — Realtime & Options

Nhánh: `tuyen/realtime-options`

### File được giao

- `src/RevenueManagement.Api/Infrastructure/Realtime/**`
- `src/RevenueManagement.Api/Options/**`
- `web/src/**`, trừ file API dashboard do B chủ trì.

### Phải hoàn thành

- Options cho Redis và Worker; bind theo environment và fail-fast khi thiếu cấu hình quan trọng.
- SignalR Hub authorize membership trước khi join room theo Group.
- Chỉ broadcast sau database commit.
- Automatic reconnect và gọi lại report snapshot sau reconnect.
- Phối hợp phần React dùng ba API REV-01/02/03; React không được tính thành nghiệp vụ mới.
- React không tự viết lại business rule của A/B/C.
- Demo hai client cùng Group nhận update và Group khác không nhận.

### Không làm

- Không đặt business rule trong React.
- Không broadcast toàn bộ client.
- Không coi SignalR event là nguồn dữ liệu cuối; sau reconnect phải resync bằng API.

## Việc chung của cả nhóm

- TECH-04: Git flow, PR và review chéo.
- TECH-05: Docker Compose, config/secret và khả năng chạy lại.
- TECH-06: unit test + integration test.
- TECH-07: structured log, health check và correlation bằng request ID.
- TECH-08: README, tài liệu và khai báo AI.
- CI phải restore, build và test trước khi merge.

## Quy tắc sửa file chung

`Program.cs`, entity, migration, DTO contract và event contract là file giao nhau. Người không sở hữu file phải:

1. Mở issue hoặc nhắn nhóm mô tả thay đổi.
2. Tạo PR riêng, không ghép với nhiều thay đổi khác.
3. Yêu cầu owner của file review.
4. Báo các module đang tiêu thụ contract trước khi merge.

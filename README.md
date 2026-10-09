# RevenueFlow

Ứng dụng quản lý doanh thu theo nhóm bằng ASP.NET Core 10, EF Core/Npgsql, PostgreSQL, Redis, React/TypeScript và SignalR. Bản tích hợp nằm trên nhánh `codex/integrate-revenueflow`, ghép `duy/core-auth`, `thanh/cache-query`, `thuw/queue-background`, `tuyen/realtime-options`.

## Phạm vi

- REV-01: CRUD giao dịch Income, xóa mềm, kiểm tra quyền nhóm và RowVersion.
- REV-02: một endpoint báo cáo lọc nhóm/thời gian/ví, groupBy Date/Wallet/Category/Member; Redis TTL và version theo nhóm.
- REV-03: CRUD quy tắc Daily/Weekly/Monthly, bật/tắt, trigger và nhật ký; Channel queue và BackgroundService.
- JWT/refresh token, quyền theo nhóm; SignalR chỉ phát đến thành viên nhóm và lấy snapshot lại khi reconnect.

## Chạy demo

Yêu cầu .NET 10 SDK, Node 22+, pnpm 11.25.0 và Docker Desktop đang chạy. Sao chép `.env.example` thành `.env`, thay POSTGRES_PASSWORD và JWT_SIGNING_KEY bằng giá trị local riêng. Không commit `.env`.

```powershell
dotnet restore RevenueManagement.slnx
./scripts/start-demo.ps1
```

Terminal thứ hai:

```powershell
cd web
pnpm install --frozen-lockfile
pnpm dev --host 127.0.0.1
```

Giao diện http://127.0.0.1:5173. Swagger http://localhost:5080/swagger. PostgreSQL demo ở cổng 55432; Redis ở 6379. Seeder chỉ chạy trong Development khi Database__InitializeDemo=true; database có user sẽ không seed lại.

Demo: demo1@revenueflow.local và demo2@revenueflow.local cùng nhóm; demo3@revenueflow.local thuộc nhóm khác. Mật khẩu demo: Demo@123456. Đây là dữ liệu thử local.

## Kiểm thử

Khi API demo đang chạy:

```powershell
./scripts/test-demo.ps1
./scripts/verify-recurring.ps1
node web/scripts/realtime-smoke.mjs
```

Script test truyền REVENUEFLOW_TEST_DATABASE, REVENUEFLOW_TEST_API và Redis riêng. Thiếu cấu hình thì integration test được đánh dấu Skip. TRX ở docs/test-results/final; kết quả recurring/realtime ở docs/evidence. Script recurring tạo và giữ dữ liệu demo để chụp ảnh; realtime xóa mềm giao dịch do chính script tạo.

```powershell
dotnet build RevenueManagement.slnx
dotnet test tests/RevenueManagement.UnitTests
cd web
pnpm build
```

CI khai báo PostgreSQL/Redis và API demo trước integration test. Kết quả local không thay thế một lần chạy GitHub Actions thực tế.

## API

| Route | Chức năng |
|---|---|
| POST /api/auth/login, POST /api/auth/refresh | Xác thực và xoay refresh token |
| GET /api/context | Nhóm, ví, danh mục được phép dùng |
| GET/POST /api/revenues, GET/PUT/DELETE /api/revenues/{id} | Giao dịch Income |
| GET /api/reports/revenue | Báo cáo tổng hợp |
| GET/POST /api/recurring-rules, GET/PUT/DELETE /api/recurring-rules/{id} | Quy tắc định kỳ |
| PATCH /api/recurring-rules/{id}/active | Bật hoặc tắt |
| POST /api/recurring-rules/{id}/trigger, GET /api/recurring-rules/{id}/runs | Chạy và xem nhật ký |
| /hubs/revenue | SignalR, JoinGroup kiểm tra quyền |
| GET /health, /health/database, /health/redis | Sức khỏe dịch vụ |

API nghiệp vụ yêu cầu Bearer token. Lỗi chung chứa code/message/requestId: 400 dữ liệu sai, 401 chưa xác thực, 403 không có quyền, 404 không tồn tại, 409 xung đột. Báo cáo có X-Report-Cache và X-Database-Query-Count; mọi request có X-Request-ID.

## Giới hạn thực tế

Migration gốc Core còn Budget, BudgetAlert và GroupInvitation cũ; bản tích hợp không mở nghiệp vụ này. Không mô tả database hiện tại là chỉ có đúng tám bảng. Bỏ bảng cũ trên database đang dùng cần migration riêng sau khi kiểm tra dữ liệu.

Channel nằm trong tiến trình; database lưu lịch/run log để scanner tìm lại kỳ đến hạn. Chưa kiểm thử nhiều instance. Cache TTL là dự phòng khi invalidation thất bại. UI hiển thị tối đa 100 giao dịch trong bộ lọc; API hỗ trợ phân trang.

Neon vẫn là lựa chọn triển khai; kiểm thử hiện tại dùng PostgreSQL local riêng. Không chạy script ghi dữ liệu demo trên Neon dùng chung.

Tài liệu: [phân công](docs/ASSIGNMENTS.md), [ma trận](docs/REQUIREMENT_MATRIX.md), [queue](docs/QUEUE_BACKGROUND.md), [AI](docs/AI_USAGE.md).

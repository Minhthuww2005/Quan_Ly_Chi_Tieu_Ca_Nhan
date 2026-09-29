# Ma trận yêu cầu → owner → source code

| Mã | Nội dung | Owner | Vị trí chính |
|---|---|---|---|
| 1A.1 | Middleware pipeline | A | `Program.cs`, `Common/Middleware` |
| 1A.2 | DI và lifetime | A | `Program.cs`, extension DI sẽ tạo |
| 1A.3 | Controller + Minimal API | A/B | `RevenueEndpoints`, `ReportsController` |
| 1A.4 | Error và HTTP status | A | `GlobalExceptionMiddleware`, `ErrorResponse` |
| 1B.1 | EF Core + migration | A | `Infrastructure/Persistence` |
| 1B.2 | AuthN/AuthZ | A | `Features/Auth`, `Infrastructure/Authorization` |
| 1B.3 | Binding và validation | C | `RevenueValidator`, DTOs |
| 1B.4 | Hosted service | C | `RecurringRevenueWorker` |
| 1B.5 | Options pattern | D | `Options` |
| 2.1 | Git | Cả nhóm | `.github`, lịch sử PR |
| 2.2 | Chạy lại | Cả nhóm | `Dockerfile`, `docker-compose.yml`, `README.md` |
| 2.3 | Config và secret | A/D | `appsettings*`, `.env.example`, `.gitignore` |
| 2.4 | Testing | Cả nhóm | `tests` |
| 2.5 | CI | Cả nhóm | `.github/workflows/ci.yml` |
| 2.6 | Bảo mật cơ bản | A | auth policy, CORS, integration tests |
| 2.7 | Logging/Health | A + cả nhóm | middleware, health checks, logging config |
| 2.8 | Tài liệu kỹ thuật | Cả nhóm | `README.md`, `docs` |
| 2.9 | Khai báo AI | Cả nhóm | `docs/AI_USAGE.md` |
| 3.1 | Cache và tối ưu query | B | `Reports`, `Caching`, EF indexes |
| 3.2 | Queue và xử lý nền | C | `Workers`, `Recurring` |
| 3.3 | SignalR realtime | D | `Infrastructure/Realtime`, React hook |

Trước khi nộp bài, thay các placeholder bằng file/module thật nhưng không đổi owner nếu nhóm chưa thống nhất.

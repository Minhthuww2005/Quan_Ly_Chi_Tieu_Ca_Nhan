# Ma trận yêu cầu → owner → source code

| Mã | Nội dung | Owner | Vị trí chính |
|---|---|---|---|
| 1A.1 | Middleware pipeline | Duy | `Program.cs`, `Common/Middleware` |
| 1A.2 | DI và lifetime | Duy | `Program.cs`, extension DI sẽ tạo |
| 1A.3 | Controller + Minimal API | Thanh, Duy phối hợp | `ReportsController`, `RevenueEndpoints` |
| 1A.4 | Error và HTTP status | Duy | `GlobalExceptionMiddleware`, `ErrorResponse` |
| 1B.1 | EF Core + migration | Duy | `Infrastructure/Persistence` |
| 1B.2 | AuthN/AuthZ | Duy | `Features/Auth`, `Infrastructure/Authorization` |
| 1B.3 | Binding và validation | Thuw | `RevenueValidator`, DTOs |
| 1B.4 | Hosted service | Thuw | `RecurringRevenueWorker` |
| 1B.5 | Options pattern | Tuyen | `Options` |
| 2.1 | Git | Cả nhóm | `.github`, lịch sử PR |
| 2.2 | Chạy lại | Thanh | `Dockerfile`, `docker-compose.yml`, hướng dẫn chạy |
| 2.3 | Config và secret | Tuyen | `appsettings*`, `.env.example`, `.gitignore` |
| 2.4 | Testing | Thuw chủ trì, cả nhóm phối hợp | `tests`; mỗi người test module mình |
| 2.5 | CI | Tuyen | `.github/workflows/ci.yml` |
| 2.6 | Bảo mật cơ bản | Tuyen, Duy phối hợp | CORS/security config; Auth/IDOR policy |
| 2.7 | Logging/Health | Thanh | logging, RequestId, health checks |
| 2.8 | Tài liệu kỹ thuật | Thanh tổng hợp, cả nhóm viết phần mình | `README.md`, `docs` |
| 2.9 | Khai báo AI | Thuw tổng hợp | `docs/AI_USAGE.md` |
| 3.1 | Cache và tối ưu query | Thanh | `Reports`, `Caching`, EF indexes |
| 3.2 | Queue và xử lý nền | Thuw | `Workers`, `Recurring` |
| 3.3 | SignalR realtime | Tuyen | `Infrastructure/Realtime`, React hook |

Trước khi nộp bài, thay các placeholder bằng file/module thật nhưng không đổi owner nếu nhóm chưa thống nhất.

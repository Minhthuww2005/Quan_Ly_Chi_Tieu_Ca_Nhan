# Kiến trúc

Backend dùng **feature folders** trong một ASP.NET Core project để giữ phạm vi đồ án gọn nhưng vẫn tách trách nhiệm OOP:

- `Domain`: entity và enum, không biết HTTP hay database.
- `Features`: use case theo ba nghiệp vụ REV-01/02/03. Endpoint mỏng, service giữ business rule.
- `Infrastructure`: EF Core, JWT/resource access, Redis, SignalR, queue/worker.
- `Common`: middleware và error contract dùng chung.
- `Options`: cấu hình strongly typed, bind từ appsettings/environment.

Frontend tách `api`, `auth`, `hooks`, `layouts`, `pages`. UI không truy cập Axios hay SignalR trực tiếp ngoài adapter/hook tương ứng.

Luồng ghi: HTTP → RevenueService → SQL commit → đổi cache version → SignalR broadcast.

Luồng định kỳ: scanner → Channel queue → worker → idempotency `(RuleId, OccurrenceDate)` → SQL commit → cache + SignalR.

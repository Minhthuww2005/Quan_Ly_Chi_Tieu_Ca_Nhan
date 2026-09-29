# Quy trình Git của nhóm

## Hệ thống branch

- `main`: bản ổn định để demo/nộp bài. Không push trực tiếp.
- `develop`: nhánh tích hợp chung. Mọi feature PR vào đây trước.
- `duy/core-auth`: Core, Auth, EF Core và Revenue CRUD.
- `thanh/report-cache`: Report query, Redis cache và benchmark.
- `thuw/recurring-worker`: Validation, recurring rule, queue và worker.
- `tuyen/realtime-ui`: SignalR, Options và React UI.

Các branch `Duy`, `Thanh`, `Thuw`, `Tuyen` cũ được giữ lại để không làm mất lịch sử. Không tiếp tục commit mới vào các branch tên cũ.

## Luồng merge

```text
duy/core-auth -----------┐
thanh/report-cache ------┤
thuw/recurring-worker ---┼── Pull Request ──> develop ── test tích hợp ──> main
tuyen/realtime-ui -------┘
```

Khi một module có task lớn, tạo branch ngắn từ branch module:

```text
duy/jwt-auth
thanh/redis-invalidation
thuw/idempotency-test
tuyen/signalr-reconnect
```

Task branch merge về branch module. Branch module tạo PR vào `develop`. Cuối tuần, `develop` đã qua integration test mới tạo PR vào `main`.

## Lệnh bắt đầu công việc

```powershell
git fetch origin
git switch develop
git pull origin develop
git switch -c thanh/report-query
```

Sau khi code:

```powershell
git add <folder-duoc-phan-cong>
git commit -m "feat(report): add revenue aggregation query"
git push -u origin thanh/report-query
```

## Definition of Done cho một PR

- Chỉ sửa folder được giao, trừ khi PR ghi rõ điểm tích hợp.
- Build và test liên quan phải pass.
- Không commit secret, `.env`, `bin`, `obj` hoặc `node_modules`.
- Route, DTO hoặc event thay đổi phải báo các module đang dùng.
- Có hướng dẫn kiểm tra và minh chứng rubric nếu liên quan.
- CODEOWNER của folder phải approve.
- Ưu tiên Squash and merge.

## File dễ conflict

`Program.cs`, entity, migration, DTO, event contract, `package.json`, Docker và appsettings là file dùng chung. Người không sở hữu file phải mở PR nhỏ và gọi owner review, không sửa kèm một feature lớn.

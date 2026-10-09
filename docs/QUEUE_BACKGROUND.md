# REV-03 — Doanh thu định kỳ, Queue & Background (C — Thuw)

Nhánh: `thuw/queue-background`. Yêu cầu phụ trách: `1B.3`, `1B.4`, `2.4`, `2.9`, `3.2`.

## Luồng xử lý

```text
RecurringRuleScanner (BackgroundService, mỗi Worker:PollIntervalSeconds)
  └─ SELECT rule IsActive và NextRunOn <= hôm nay (theo TimeZoneId của rule)
  └─ RevenueJobQueue.EnqueueAsync(ruleId)  ── Channel<T> bounded, không nhận trùng ruleId đang chờ
        │
RecurringRevenueWorker (BackgroundService, consumer)
  └─ scope DI riêng → RecurringRevenueProcessor.ProcessRuleAsync(ruleId)
        mỗi kỳ đến hạn, trong MỘT database transaction:
          RunLog(RuleId, OccurrenceDate) đã có? → bỏ qua (AlreadyProcessed)
          điều kiện không hợp lệ?            → RunLog Skipped
          ngược lại                          → Transaction(Income, RecurringRuleId, IdempotencyKey) + RunLog Created
          LastRunOn = kỳ này, NextRunOn = CalculateNextRunOn(...), RowVersion++
          COMMIT
        sau commit → IRevenueChangedPublisher.PublishAsync(groupId)  (B invalidate cache, D SignalR)
  └─ lỗi → retry exponential backoff (2s, 4s, 8s... trần 60s, jitter ≤ 20%), không chặn job khác
  └─ hết Worker:MaxRetryAttempts → RunLog Failed (Error ≤ 1000 ký tự) + dời NextRunOn sang kỳ sau
```

## Idempotency (chạy cùng occurrence hai lần chỉ tạo một transaction)

1. Hàng đợi giữ chỗ theo `RuleId` cho đến khi job xong → scanner không đẩy trùng.
2. Trong transaction, kiểm tra `RecurringRunLog (RuleId, OccurrenceDate)` trước khi tạo.
3. Database: unique index `uq_recurring_rule_occurrence (recurring_rule_id, occurrence_date)` và unique
   `transactions.idempotency_key = "rr:{ruleId}:{yyyyMMdd}"`. Nếu hai worker chạy song song, bên commit sau
   nhận lỗi `23505` và processor coi kỳ đó là đã xử lý.
4. `recurring_rules.row_version` là concurrency token, nên hai bên không ghi đè `NextRunOn` của nhau.

## Quy tắc lịch (`RecurringSchedule`)

| Frequency | Lần đầu | Lần kế |
|---|---|---|
| Daily | StartDate | + Interval ngày |
| Weekly | Ngày `DayOfWeek` đầu tiên ≥ StartDate (ISO: 1 = Thứ Hai … 7 = Chủ nhật; mặc định thứ của StartDate) | + 7 × Interval ngày |
| Monthly | Ngày `DayOfMonth` (mặc định ngày của StartDate) trong tháng của StartDate, hoặc tháng sau nếu đã qua | + Interval tháng |

- Tháng thiếu ngày 29/30/31: chạy vào ngày cuối tháng, tháng sau quay lại ngày neo (31/01 → 28/02 → 31/03).
- Không có lần chạy nào sau `EndDate` (`NextRunOn = null`).
- "Hôm nay" tính theo `TimeZoneId` của quy tắc (mặc định `Asia/Ho_Chi_Minh`).
- Tạo mới, sửa lịch hoặc bật lại: chạy từ hôm nay, **không chạy bù** quá khứ.
  Server tắt vài ngày thì worker bù tối đa `Worker:MaxCatchUpOccurrences` kỳ mỗi lần.

## API (`/api/recurring-rules`, yêu cầu JWT)

| Method | Route | Kết quả |
|---|---|---|
| GET | `/api/recurring-rules?walletId=` | 200 danh sách quy tắc trong các nhóm user là thành viên |
| GET | `/api/recurring-rules/{id}` | 200 / 404 |
| POST | `/api/recurring-rules` | 201 / 400 / 404 |
| PUT | `/api/recurring-rules/{id}` | 200 / 400 / 404 / 409 (RowVersion lệch) |
| PATCH | `/api/recurring-rules/{id}/active` | 200 / 404 / 409 |
| DELETE | `/api/recurring-rules/{id}` | 204 / 404 / 409 (đã có nhật ký → hãy tắt) |
| GET | `/api/recurring-rules/{id}/runs?status=&from=&to=&limit=` | 200 nhật ký Created/Skipped/Failed |
| POST | `/api/recurring-rules/{id}/trigger` | 202 đưa vào hàng đợi ngay (demo) |

Quy tắc của nhóm khác luôn trả **404** (không tiết lộ là có tồn tại) — chống IDOR.

Ví dụ tạo quy tắc tiền thuê nhà ngày 5 hàng tháng:

```json
POST /api/recurring-rules
{
  "walletId": 1,
  "categoryId": 3,
  "amount": 5000000,
  "note": "Tiền thuê nhà",
  "frequency": "Monthly",
  "dayOfMonth": 5,
  "startDate": "2026-10-01"
}
```

## Validation (1B.3)

- Binding/DataAnnotations trên DTO; `[ApiController]` tự trả `400 ValidationProblemDetails`.
- Ràng buộc chéo (`IValidatableObject` → `RecurringRuleRequestValidator`): Amount > 0 và tối đa 4 chữ số
  thập phân, `EndDate ≥ StartDate`, `DayOfWeek` chỉ cho Weekly, `DayOfMonth` chỉ cho Monthly, TimeZoneId tồn tại.
- `RevenueValidator` (dùng chung với REV-01 của A): user là thành viên của Group sở hữu Wallet, Wallet chưa lưu trữ,
  Category thuộc đúng Wallet, chưa lưu trữ và là Income.

## Cấu hình (`Worker` section, override bằng environment `Worker__...`)

| Key | Mặc định | Ý nghĩa |
|---|---|---|
| `Enabled` | true | Tắt scanner/worker (integration test không có DB) |
| `PollIntervalSeconds` | 30 | Chu kỳ quét |
| `MaxRetryAttempts` | 3 | Số lần retry sau lần đầu |
| `BaseRetryDelaySeconds` / `MaxRetryDelaySeconds` | 2 / 60 | Backoff |
| `QueueCapacity` | 500 | Sức chứa Channel (đầy thì scanner chờ) |
| `ScanBatchSize` | 200 | Số rule tối đa mỗi lần quét |
| `MaxCatchUpOccurrences` | 31 | Số kỳ bù tối đa mỗi job |

Options được validate khi khởi động (`ValidateOnStart`): cấu hình sai thì app dừng ngay.

## Test (2.4)

```powershell
dotnet test tests/RevenueManagement.UnitTests
```

| File | Nội dung |
|---|---|
| `RecurringScheduleTests` | Daily/Weekly/Monthly, Interval, ngày 29/30/31, năm nhuận, qua năm, EndDate, múi giờ |
| `RecurringRuleRequestValidatorTests` | Binding/validation request → 400 |
| `RevenueValidatorTests` | Amount, ngày, Note, Wallet/Category cùng Group, Income, thành viên đã rời nhóm |
| `RevenueJobQueueTests` | Không nhận trùng, requeue khi retry, backpressure; `RetryBackoffTests` |
| `RecurringRevenueProcessorTests` | **Chạy 2 lần = 1 transaction**, worker cũ đọc NextRunOn lệch, chạy bù, Skipped, Failed |
| `RecurringRuleServiceTests` | Tạo/sửa/bật lại không chạy bù, IDOR 404, xóa có nhật ký 409, trigger |

Test dùng EF Core InMemory: không ép unique index và bỏ qua transaction. Lớp chống trùng bằng unique index
(mục 3 ở trên) cần kiểm tra thêm trên PostgreSQL thật — xem phần minh chứng.

## Minh chứng cần chụp khi demo

1. Tạo rule `Daily`, `startDate` = hôm nay → gọi `POST /{id}/trigger` hai lần → `GET /{id}/runs` chỉ có một
   `Created`, bảng `transactions` chỉ có một dòng với `idempotency_key = rr:{id}:{yyyyMMdd}`.
2. Log retry: ngắt mạng tạm thời trong lúc worker đang xử lý → log
   `failed on attempt 1; retry 1/3 in 00:00:02...`, sau đó `marked Failed` nếu vẫn lỗi.
3. Dừng app (Ctrl+C) → log `Recurring worker stopped` / `Recurring scanner stopped` (graceful cancellation).
4. Gửi `amount: 0`, `dayOfWeek` cho rule Monthly → 400 kèm lỗi từng trường.

Không ghi số liệu giả; chỉ dán log/ảnh từ lần chạy thật.

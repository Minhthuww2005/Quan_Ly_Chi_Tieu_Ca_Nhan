# Khai báo sử dụng AI

AI được dùng để gợi ý cấu trúc project, boilerplate, test và tài liệu. Nhóm phải review, chạy test và có thể giải thích từng phần trước khi nộp. Không dùng số benchmark giả; latency, P95, query count và log retry chỉ ghi sau khi đo thật.

## Chi tiết theo module

Mỗi thành viên bổ sung một mục cho module mình theo mẫu dưới đây. Thuw tổng hợp (2.9).

| Module | Người phụ trách | Công cụ AI | AI hỗ trợ phần nào | Người đã review/sửa/kiểm chứng |
|---|---|---|---|---|
| REV-03 Recurring, Queue & Background (`Features/Recurring`, `Infrastructure/Workers`, `RevenueValidator`, unit test liên quan) | Thuw | Claude (Anthropic) | Bản nháp code scanner/worker/processor, `CalculateNextRunOn`, validation, unit test và `docs/QUEUE_BACKGROUND.md` | _Thuw điền sau khi tự chạy `dotnet test`, demo và đọc lại code_ |

Ghi chú cho REV-03: các quyết định nghiệp vụ (không chạy bù khi tạo/bật lại, tháng thiếu ngày chạy vào ngày cuối tháng, Failed thì dời lịch) cần được nhóm xác nhận; người phụ trách phải giải thích được luồng idempotency và retry khi vấn đáp.

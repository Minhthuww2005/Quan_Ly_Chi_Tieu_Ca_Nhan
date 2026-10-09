# Kiểm chứng bản tích hợp RevenueFlow

Ngày chạy: 10/10/2026, môi trường Windows local. Nhánh: `codex/integrate-revenueflow`. Nguồn được ghép: Core/Auth `e6f801b`, Cache/Query `0d3d08b`, Queue/Background `41bb870`, Realtime/Options `a8fc988`.

## Kết quả thực tế

| Kiểm tra | Kết quả |
|---|---|
| Build backend và test projects | Thành công, 0 lỗi; còn 17 cảnh báo xUnit1051 về cancellation token trong test HTTP |
| TypeScript và Vite production build | Thành công; Rollup bỏ hai annotation của thư viện SignalR |
| Unit test | 126 đạt, 0 lỗi, 0 bỏ qua |
| Integration test | 4 đạt, 0 lỗi, 0 bỏ qua trên PostgreSQL và Redis thật local |
| Health | Database Healthy, Redis Healthy |
| Recurring smoke | Rule 2, hai trigger, một Created run, transaction 7 |
| SignalR smoke | Hai thành viên nhận cập nhật; người ngoài bị chặn JoinGroup, nhóm khác nhận 0 event; reconnect/rejoin/snapshot đạt |

TRX cuối: `docs/test-results/final/thanh_DESKTOP-RG0CDDT_2026-10-10_02_28_15.trx` và `thanh_DESKTOP-RG0CDDT_2026-10-10_02_28_18.trx`. Dữ liệu test và ảnh ở thư mục gitignored `docs/evidence`.

Benchmark đo thật trong integration test: cache miss n=10, average 40.13 ms, P95 105.04 ms, 3 truy vấn; cache hit n=30, average 13.47 ms, P95 60.16 ms, 1 truy vấn. Hit vẫn kiểm tra quyền thành viên. Đây là số đo ở database demo nhỏ trên máy local, không đại diện tải production; không kết luận hiệu quả index riêng từ số đo này.

Test HTTP kiểm tra CRUD, dữ liệu sai 400, thiếu token 401, người ngoài 403, không tồn tại 404, RowVersion cũ 409, cache sau update và refresh token không tái sử dụng. Test refresh đồng thời xác nhận chỉ một trong hai yêu cầu nhận 200, yêu cầu còn lại nhận 401. Unit test bổ sung kiểm tra thứ tự middleware, DI lifetime sai và snapshot cũ không ghi vào version cache mới.

## Đối chiếu workbook

REV-01, REV-02 và REV-03 đã có API và giao diện demo. Bộ lọc ví có trong API; UI hiện lọc nhóm và khoảng thời gian, chọn bốn cách groupBy. UI hiển thị tối đa 100 giao dịch, API có phân trang. Auth, EF migration, validation dùng chung, Options ValidateOnStart, worker, Redis version/TTL và hub authorization đã được tích hợp.

Các yêu cầu về quy trình Git/PR/review và lần chạy CI thực tế cần minh chứng trên GitHub sau khi đẩy nhánh. Workflow đã cấu hình nhưng chưa được coi là CI pass. Owner không thay đổi so với bảng phân công. Logging sử dụng JsonConsole có cấu trúc và RequestId, chưa dùng Serilog theo ví dụ trong workbook.

Schema baseline Core còn bảng ngoài tám entity nghiệp vụ đã chốt, gồm Budget/BudgetAlert/GroupInvitation; không có nghiệp vụ tương ứng trong bản demo. Không khẳng định đã đạt yêu cầu đúng tám entity/schema. Channel không bền vững qua restart và chưa được kiểm thử nhiều instance. Những điểm này cần được trình bày đúng trong báo cáo.

## Tư liệu báo cáo

Đã đọc hai PDF người dùng gửi và lưu phong cách của báo cáo Word mẫu trong `REPORT_STYLE_REFERENCE.md`. Ảnh chụp thực tế hiện có: đăng nhập, dashboard, doanh thu, định kỳ và nhật ký Created. Báo cáo chương 3–6 sẽ dựa trên bản tích hợp này; không sử dụng mô tả skeleton/TODO của tài liệu 3.3 cũ để khẳng định trạng thái hiện tại.
